// Usings globais
using BalgImport.Hubs;
using BalgImport.Models;
using BalgImport.Services;
using CsvHelper.Configuration.Attributes;
using CsvHelper.Configuration;
using CsvHelper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.IIS;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Projeto_BALG_Import.Controllers;
using Projeto_BALG_Import.Services;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using System;

// ========== Program.cs ==========
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
}); 

// Configuração do CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// Configuração do SignalR
builder.Services.AddSignalR();

// Configuração do Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1024 * 1024 * 1024; // 1GB
});

// Configuração do IIS
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 1024 * 1024 * 1024; // 1GB
});

// Configuração do Form
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 1024 * 1024 * 1024; // 1GB
});

// Registra os serviços
builder.Services.AddScoped<ImportacaoService>();
builder.Services.AddScoped<TestDataGenerator>();
builder.Services.AddSingleton<IStorageService, StorageService>();
builder.Services.AddSingleton<IUploadService, UploadService>();
builder.Services.AddScoped<IImportacaoService, ImportacaoService>();
builder.Services.AddScoped<StatusImportacaoService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Upload}/{action=Index}/{id?}");

// Mapeia o hub do SignalR
app.MapHub<UploadHub>("/uploadHub");
app.MapHub<ImportacaoHub>("/importacaoHub");
app.MapHub<SignalRHub>("/signalr");

app.Run();

// ========== Models\DashboardViewModel.cs ==========
namespace BalgImport.Models
{
    public class DashboardViewModel
    {
        public List<LoteViewModel> Lotes { get; set; } = new List<LoteViewModel>();
        public IndicadoresViewModel Indicadores { get; set; } = new IndicadoresViewModel();
    }

    public class LoteViewModel
    {
        public Guid Id { get; set; }
        public DateTime DataCriacao { get; set; }
        public DateTime? DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
        public string UsuarioId { get; set; }
        public string UsuarioNome { get; set; }
        public string Status { get; set; }
        public int TotalArquivos { get; set; }
        public int ArquivosProcessados { get; set; }
        public int ArquivosComErro { get; set; }
        public string MensagemErro { get; set; }
    }

    public class IndicadoresViewModel
    {
        public int TotalLotes { get; set; }
        public int TotalArquivos { get; set; }
        public int ArquivosProcessados { get; set; }
        public int ArquivosComErro { get; set; }
        public int LotesEmAndamento { get; set; }
        public int LotesConcluidos { get; set; }
        public int LotesComErro { get; set; }
    }
}

// ========== Models\ErrorViewModel.cs ==========
namespace BalgImport.Models
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}

// ========== Models\StatusUpdate.cs ==========
namespace BalgImport.Models
{
    public class StatusUpdate
    {
        public long idUpload { get; set; }
        public int IdLote { get; set; }
        public int IdArquivo { get; set; }
        public string NomeArquivo { get; set; }
        public string Etapa { get; set; }
        public string Status { get; set; }
        public string Mensagem { get; set; }
        public DateTime DataHora { get; set; }
        public int TotalArquivos { get; set; }
        public int ArquivosProcessados { get; set; }
    }
}

// ========== Models\StatusUpload.cs ==========
namespace BalgImport.Models
{
    public class StatusUpload
    {
        public string NomeArquivo { get; set; }
        public string Status { get; set; }
        public string Mensagem { get; set; }
        public int Progresso { get; set; }
        public DateTime DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
        public Guid BatchId { get; set; }
        public int Tentativas { get; set; }
        public string CaminhoArquivo { get; set; }
        public long TamanhoArquivo { get; set; }
        public string HashArquivo { get; set; }
    }
}

// ========== Models\UploadBatch.cs ==========
namespace BalgImport.Models
{
    public class UploadBatch
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string UsuarioId { get; set; }
        public string UsuarioNome { get; set; }
        public DateTime DataCriacao { get; set; } = DateTime.Now;
        public string Status { get; set; } = "PENDENTE";
        public DateTime DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
        public List<UploadStatus> Arquivos { get; set; } = new List<UploadStatus>();
        public string? MensagemErro { get; set; }
        public int TotalArquivos => Arquivos.Count;
        public int ArquivosProcessados => Arquivos.Count(a => a.Status == "CONCLUIDO");
        public int ArquivosComErro => Arquivos.Count(a => a.Status == "ERRO");

        public UploadBatch()
        {
            Status = "PENDENTE";
        }
    }
}

// ========== Models\UploadStatus.cs ==========
namespace BalgImport.Models
{
    public class UploadStatus
    {
        public string NomeArquivo { get; set; }
        public long Tamanho { get; set; }
        public string Status { get; set; }
        public string Hash { get; set; }
        public DateTime DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
        public string MensagemErro { get; set; }
    }
}

// ========== Hubs\ImportacaoHub.cs ==========
namespace BalgImport.Hubs
{
    public class UploadHub : Hub
    {
        public override Task OnConnectedAsync()
        {
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            return base.OnDisconnectedAsync(exception);
        }

        public async Task StatusChanged(string batchId, string status, int arquivosProcessados, int totalArquivos)
        {
            await Clients.All.SendAsync("statusChanged", new
            {
                batchId,
                status,
                arquivosProcessados,
                totalArquivos
            });
        }
    }
} 


namespace BalgImport.Hubs
{
    public class SignalRHub : Hub
    {
        public async Task AtualizarStatus(object status)
        {
            await Clients.All.SendAsync("AtualizarStatus", status);
        }
    }
} 

namespace BalgImport.Hubs
{
    public class ImportacaoHub : Hub
    {
        private readonly ILogger<ImportacaoHub> _logger;

        public ImportacaoHub(ILogger<ImportacaoHub> logger)
        {
            _logger = logger;
        }

        public async Task AtualizarStatus(object status)
        {
            _logger.LogInformation($"SignalR: Recebido status: {status}");
            await Clients.All.SendAsync("AtualizarStatus", status);
        }

        public async Task AtualizarResumo(object resumo)
        {
            _logger.LogInformation($"SignalR: AtualizarResumo: {resumo}");
            await Clients.All.SendAsync("AtualizarResumo", resumo);
        }

        public async Task AtualizarLoteAtual(object loteAtual)
        {
            _logger.LogInformation($"SignalR: AtualizarLoteAtual: {loteAtual}");
            await Clients.All.SendAsync("AtualizarLoteAtual", loteAtual);
        }

        public async Task AdicionarHistoricoLote(object loteFinalizado)
        {
            _logger.LogInformation($"SignalR: AdicionarHistoricoLote: {loteFinalizado}");
            await Clients.All.SendAsync("AdicionarHistoricoLote", loteFinalizado);
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation($"SignalR: Cliente conectado: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation($"SignalR: Cliente desconectado: {Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }
    }
}

// ========== Services\ImportacaoService.cs ==========
namespace Projeto_BALG_Import.Services
{
    #region Models

    public class Arquivo: ArquivoBase
    {
        
        public string FileName { get; set; }
        public IFormFile FormFile { get; internal set; }
    }

    public abstract class ArquivoBase
    {
        public int idUpload { get; set; }
        
        public DateTime DataImportacao { get; set; }
        public bool FlagErro { get; set; }
        public string MensagemErro { get; set; }
        public string Status { get; set; }
    }

    public class LayoutConfig
    {
        public int LinhasCabecalho { get; set; }
        public string Delimiter { get; set; }
        public Dictionary<string, (int Index, Type Tipo)> MapeamentoCampos { get; set; }
        public CultureInfo Cultura { get; set; }
    }

    public class LoteProcessamento
    {
        public LoteProcessamento()
        {
                Arquivos = new List<Arquivo>();
        }
        public int IdLote { get; set; }
        public List<Arquivo> Arquivos { get; set; }
        public string Status { get; set; }
        public DateTime DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
        public string Mensagem { get; set; }
        public string codProc { get; internal set; }
        public int idArq { get; internal set; }
        public int idUpload { get; internal set; }
        public int ArquivosProcessados
        {
            get
            {
                return this.Arquivos.Count(a => a.Status == StatusTipos.Concluido);
            }
        }

        public string Usuario { get;  set; }
    }

    public class ArquivoBALG 
    {
        [Name("0FISCPER")]
        [Index(0)]
        public string D_BASE_STR { get; set; }

        [Name("0COMPANY")]
        [Index(1)]
        public int CD_EMP { get; set; }

        [Name("CA_CINTER")]
        [Index(2)]
        public string CD_CONTA { get; set; }

        [Name("CA_PRAZO")]
        [Index(3)]
        public int PRZ { get; set; }

        [Name("0CURKEY_GC")]
        [Index(4)]
        public string MOE { get; set; }

        [Name("0CS_TRN_GC")]
        [Index(5)]
        public decimal SLD { get; set; }

        public DateTime D_BASE => DateTime.ParseExact(D_BASE_STR, "MMyyyy", CultureInfo.InvariantCulture);
    }

    #endregion

    public interface IImportacaoService
    {
        Task<int> ProcessarDaPastaAsync();
        Task<List<LoteProcessamento>> DividirEmLotesAsync(List<Arquivo> arquivos);
        Task<LoteProcessamento> ProcessarLoteAsync(LoteProcessamento lote);
    }

    public class ImportacaoService : IImportacaoService
    {
        private readonly string _connectionString;
        private readonly string _pastaOrigem;
        private const int TAMANHO_LOTE = 25;
        private readonly ILogger<ImportacaoService> _logger;
        private readonly IHubContext<SignalRHub> _hubContext;
        private readonly StatusImportacaoService _statusService;


        public ImportacaoService(IConfiguration configuration, ILogger<ImportacaoService> logger, IHubContext<SignalRHub> hubContext, StatusImportacaoService statusService)
        {
            _connectionString = $"Server=(localdb)\\mssqllocaldb;Database=DB_CONSOLIDADO;Trusted_Connection=True;MultipleActiveResultSets=true";
            _pastaOrigem = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
            _logger = logger;
            _hubContext = hubContext;
            _statusService = statusService;
        }

        #region Processamento via API

        private async Task ProcessarArquivoIFormFileAsync(Arquivo arquivo, LoteProcessamento lote)
        {
            int idUpload = lote.IdLote;

            _logger.LogInformation($"{lote.codProc} -> Iniciando leitura do arquivo IFormFile {arquivo.FileName}");

            try
            {
                using (var stream = arquivo.FormFile.OpenReadStream())
                {
                    await ProcessarArquivoAsync(stream, arquivo.FileName, lote);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{lote.codProc} -> Erro ao processar arquivo {arquivo.FileName}");
                throw;
            }
        }

        #endregion

        #region Processamento via Pasta

        public async Task<int> ProcessarDaPastaAsync()
        {
            int idUpload = await CriarControleDeUploadAsync(0, "PASTA_UPLOAD");

            var arquivos = Directory.GetFiles(_pastaOrigem, "*.csv");
            var lote = new LoteProcessamento { IdLote = idUpload };

            var tasks = arquivos.Select(arquivo => ProcessarArquivoAsync(arquivo, lote));
            await Task.WhenAll(tasks);

            await AtualizarControleDeUploadAsync(idUpload, "FINALIZADO", "Todos os arquivos da pasta processados.");
            return idUpload;
        }

        #endregion

        #region Processamento Individual do Arquivo

        private async Task ProcessarArquivoAsync(Stream stream, string nomeArquivo, LoteProcessamento lote)
        {
            int idUpload = lote.IdLote;
            _logger.LogInformation($"{lote.codProc} -> Iniciando processamento do arquivo {nomeArquivo} (stream)");

            try
            {
                var dados = await LerCsvComDuploCabecalhoAsync(stream);
                _logger.LogInformation($"{lote.codProc} -> # 01 - CSV lido com sucesso. {dados.Count} registros encontrados");

                var dt = GerarDataTable(dados, idUpload, nomeArquivo);
                _logger.LogInformation($"{lote.codProc} -> DataTable gerado com {dt.Rows.Count} linhas");

                await ProcessarNoSQLAsync(dt, lote, nomeArquivo);
                _logger.LogInformation($"{lote.codProc} -> # 04 - Arquivo {nomeArquivo} processado com sucesso no SQL");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{lote.codProc} -> Erro ao processar arquivo {nomeArquivo}");
                throw;
            }
        }

        private async Task ProcessarArquivoAsync(string caminhoArquivo, LoteProcessamento lote)
        {
            var nomeArquivo = Path.GetFileName(caminhoArquivo);
            int idUpload = lote.IdLote;

            _logger.LogInformation($"{lote.codProc} -> Iniciando processamento do arquivo {nomeArquivo}");

            try
            {
                using (var stream = new FileStream(caminhoArquivo, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var dados = await LerCsvComDuploCabecalhoAsync(stream);
                    _logger.LogInformation($"{lote.codProc} -> # 01 - CSV lido com sucesso. {dados.Count} registros encontrados");

                    var dt = GerarDataTable(dados, idUpload, nomeArquivo);
                    _logger.LogInformation($"{lote.codProc} -> DataTable gerado com {dt.Rows.Count} linhas");

                    await ProcessarNoSQLAsync(dt, lote, nomeArquivo);
                    _logger.LogInformation($"{lote.codProc} -> # 04 - Arquivo {nomeArquivo} processado com sucesso no SQL");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{lote.codProc} -> Erro ao processar arquivo {nomeArquivo}");
                throw;
            }
        }

        #endregion

        #region Leitura CSV com Detecção de Layout

        private async Task<List<ArquivoBALG>> LerCsvComDuploCabecalhoAsync(Stream stream)
        {
            using var reader = new StreamReader(stream, leaveOpen: true);

            // Detecta o layout do arquivo
            var layout = await DetectarLayoutAsync(reader);

            // Volta o stream para o início
            stream.Position = 0;
            reader.DiscardBufferedData();

            using var csv = new CsvReader(reader, new CsvConfiguration(layout.Cultura)
            {
                Delimiter = layout.Delimiter,
                Mode = CsvMode.RFC4180,
                CacheFields = true,
                BufferSize = 8192,
                HasHeaderRecord = false
            });

            // Pula as linhas de cabeçalho
            for (int i = 0; i < layout.LinhasCabecalho; i++)
            {
                await csv.ReadAsync();
            }

            try
            {
                var records = csv.GetRecords<ArquivoBALG>().ToList();
                return records;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private async Task<LayoutConfig> DetectarLayoutAsync(StreamReader reader)
        {
            var layout = new LayoutConfig
            {
                LinhasCabecalho = 2, // Default
                Delimiter = ";",     // Default
                MapeamentoCampos = new Dictionary<string, (int Index, Type Tipo)>(),
                Cultura = new CultureInfo("pt-BR") // Default
            };

            try
            {
                // Lê as primeiras linhas para análise
                var linhas = new List<string>();
                for (int i = 0; i < 3; i++) // Lê até 5 linhas para análise
                {
                    var linha = await reader.ReadLineAsync();
                    if (linha == null) break;
                    linhas.Add(linha);
                }

                if (linhas.Count < 3) return layout;

                // Detecta o delimitador
                layout.Delimiter = DetectarDelimitador(linhas);

                // Detecta número de linhas de cabeçalho
                //layout.LinhasCabecalho = DetectarLinhasCabecalho(linhas, layout.Delimiter);

                // Detecta a cultura baseado nos números
                layout.Cultura = DetectarCulturaNumerica(linhas[layout.LinhasCabecalho]);

                // Mapeia os campos baseado no cabeçalho
                layout.MapeamentoCampos = MapearCampos(linhas[layout.LinhasCabecalho - 1], layout.Delimiter);

                return layout;
            }
            catch
            {
                return layout; // Retorna configuração default em caso de erro
            }
        }

        private string DetectarDelimitador(List<string> linhas)
        {
            var delimitadores = new[] { ";", ",", "\t", "|" };
            var contagem = new Dictionary<string, int>();

            foreach (var linha in linhas)
            {
                foreach (var delim in delimitadores)
                {
                    var count = linha.Split(delim).Length;
                    if (count > 1)
                    {
                        if (!contagem.ContainsKey(delim))
                            contagem[delim] = 0;
                        contagem[delim]++;
                    }
                }
            }

            return contagem.OrderByDescending(x => x.Value).FirstOrDefault().Key ?? ";";
        }

        private int DetectarLinhasCabecalho(List<string> linhas, string delimiter)
        {
            // Verifica se a linha contém apenas texto (provavelmente cabeçalho)
            for (int i = 0; i < linhas.Count; i++)
            {
                var campos = linhas[i].Split(delimiter);
                if (campos.All(c => !c.Any(char.IsDigit)))
                    continue;
                return i;
            }
            return 0;
        }

        private Dictionary<string, (int Index, Type Tipo)> MapearCampos(string linhaCabecalho, string delimiter)
        {
            var campos = linhaCabecalho.Split(delimiter);
            var mapeamento = new Dictionary<string, (int Index, Type Tipo)>();

            for (int i = 0; i < campos.Length; i++)
            {
                var campo = campos[i].Trim();
                if (string.IsNullOrEmpty(campo)) continue;

                // Mapeia o tipo baseado no nome do campo
                var tipo = campo.ToUpper() switch
                {
                    var c when c.Contains("DATA") || c.Contains("DT_") => typeof(DateTime),
                    var c when c.Contains("VALOR") || c.Contains("SLD") => typeof(decimal),
                    var c when c.Contains("COD") || c.Contains("ID") => typeof(int),
                    _ => typeof(string)
                };

                mapeamento[campo] = (i, tipo);
            }

            return mapeamento;
        }

        private CultureInfo DetectarCulturaNumerica(string linha)
        {
            if (string.IsNullOrEmpty(linha)) return new CultureInfo("pt-BR");

            var campos = linha.Split(';');
            foreach (var campo in campos)
            {
                // Procura por números com vírgula ou ponto
                if (campo.Contains(",") && !campo.Contains("."))
                    return new CultureInfo("pt-BR");
                else if (campo.Contains(".") && !campo.Contains(","))
                    return new CultureInfo("en-US");
            }

            return new CultureInfo("pt-BR"); // Default para pt-BR
        }

        #endregion

        #region DataTable para Bulk

        private DataTable GerarDataTable<T>(List<T> dados, int idUpload, string nomeArquivo) where T : ArquivoBALG
        {

            var dt = new DataTable();

            // Colunas exatamente como na tabela STG_BALG
            dt.Columns.Add("C_ID_UPLOAD", typeof(int));
            dt.Columns.Add("R_NOME_ARQ", typeof(string));
            dt.Columns.Add("D_BASE", typeof(DateTime));
            dt.Columns.Add("CD_EMP", typeof(string));
            dt.Columns.Add("CD_CONTA", typeof(string));
            dt.Columns.Add("PRZ", typeof(string));
            dt.Columns.Add("MOE", typeof(string));
            dt.Columns.Add("SLD", typeof(decimal));
            dt.Columns.Add("DT_IMPORTACAO", typeof(DateTime));
            dt.Columns.Add("FLAG_ERRO", typeof(bool));
            dt.Columns.Add("MENSAGEM_ERRO", typeof(string));

            foreach (var item in dados)
            {
                var row = dt.NewRow();
                row["C_ID_UPLOAD"] = idUpload;
                row["R_NOME_ARQ"] = nomeArquivo;
                row["D_BASE"] = item.D_BASE;
                row["CD_EMP"] = item.CD_EMP;
                row["CD_CONTA"] = item.CD_CONTA;
                row["PRZ"] = item.PRZ;
                row["MOE"] = item.MOE;
                row["SLD"] = item.SLD;
                row["DT_IMPORTACAO"] = DateTime.Now;
                row["FLAG_ERRO"] = false;
                row["MENSAGEM_ERRO"] = DBNull.Value;
                dt.Rows.Add(row);
            }

            return dt;


            //var dt = new DataTable();
            //var tipo = typeof(T);

            //// Adiciona colunas dinamicamente baseadas nas propriedades da classe
            //foreach (var prop in tipo.GetProperties())
            //{
            //    // Converte o tipo da propriedade para o tipo do DataTable
            //    Type columnType = prop.PropertyType;
            //    if (prop.PropertyType == typeof(DateTime?))
            //        columnType = typeof(DateTime);
            //    else if (prop.PropertyType == typeof(int?))
            //        columnType = typeof(int);
            //    else if (prop.PropertyType == typeof(decimal?))
            //        columnType = typeof(decimal);

            //    dt.Columns.Add(prop.Name, columnType);
            //}

            //foreach (var item in dados)
            //{
            //    var row = dt.NewRow();

            //    // Preenche todas as propriedades dinamicamente
            //    foreach (var prop in tipo.GetProperties())
            //    {
            //        var valor = prop.GetValue(item);
            //        if (valor == null)
            //        {
            //            row[prop.Name] = DBNull.Value;
            //        }
            //        else
            //        {
            //            row[prop.Name] = valor;
            //        }
            //    }

            //    // Atualiza campos padrão
            //    row["IdUpload"] = idUpload;
            //    row["NomeArquivo"] = nomeArquivo;
            //    row["DataImportacao"] = DateTime.Now;
            //    row["FlagErro"] = false;
            //    row["MensagemErro"] = DBNull.Value;

            //    dt.Rows.Add(row);
            //}

            //return dt;
        }

        #endregion

        #region SQL - Bulk + Procedure + Transaction

        private async Task AtualizarLogProcessamentoAsync(SqlConnection conn, LoteProcessamento lote, string etapa, string status, string mensagem, SqlTransaction transaction = null)
        {
            int idUpload = lote.IdLote;

            using var cmd = new SqlCommand("PROC_ATUALIZA_LOG_PROCESSAMENTO", conn, transaction)
            {
                CommandType = CommandType.StoredProcedure
            };


            cmd.Parameters.AddWithValue("@C_ID_UPLOAD", lote.idUpload);
            cmd.Parameters.AddWithValue("@C_ID_LOTE", lote.IdLote);
            cmd.Parameters.AddWithValue("@C_ID_ARQ", lote.idArq);
            cmd.Parameters.AddWithValue("@ETAPA", etapa);
            cmd.Parameters.AddWithValue("@STATUS", status);
            cmd.Parameters.AddWithValue("@MENSAGEM", mensagem);
            cmd.Parameters.AddWithValue("@R_NOME_ARQ", lote.codProc);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation($"{lote.codProc} -> Log atualizado: {etapa} - {status}");

            // Notifica via SignalR
            try
            {
                //var statusUpdate = new
                //{
                //    idUpload = idUpload,
                //    idLote = lote.IdLote,
                //    idArquivo = lote.idArq,
                //    nomeArquivo = lote.codProc,
                //    etapa = etapa,
                //    status = status,
                //    mensagem = mensagem,
                //    dataHora = DateTime.Now,
                //    totalArquivos = lote.Arquivos?.Count ?? 0,
                //    arquivosProcessados = lote.Arquivos?.Count ?? 0
                //};

                _logger.LogInformation($"SignalR: Enviando atualização - {lote.codProc} - {status}");

//                await _statusService.EnviarStatus(lote, etapa, status, mensagem);


                //await _hubContext.Clients.All.SendAsync("ReceberStatusImportacao", statusUpdate);
                _logger.LogInformation($"SignalR: Atualização enviada com sucesso");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR: Erro ao enviar atualização");
            }
        }

        private async Task ProcessarNoSQLAsync(DataTable dt, LoteProcessamento lote, string nomeArquivo)
        {
            int idUpload = lote.IdLote;
            _logger.LogInformation($"{lote.codProc} -> Iniciando processamento SQL para {nomeArquivo}");

            using var conn = new SqlConnection(_connectionString);
            try
            {
                await conn.OpenAsync();
                _logger.LogInformation($"{lote.codProc} -> Conexão com banco de dados estabelecida");



                using (var transaction = conn.BeginTransaction(IsolationLevel.ReadCommitted))
                {


                    try
                    {
                        // Cria tabela temporária com a mesma estrutura da STG_BALG
                        var tempTableName = $"#STG_BALG_{idUpload}_{DateTime.Now.Ticks}";
                        _logger.LogInformation($"{lote.codProc} -> Criando tabela temporária {tempTableName}");

                        using (var cmd = new SqlCommand($@"
                    CREATE TABLE {tempTableName} (
                        C_ID_UPLOAD INT,
                        R_NOME_ARQ VARCHAR(255),
                        D_BASE DATE,
                        CD_EMP VARCHAR(20),
                        CD_CONTA VARCHAR(50),
                        PRZ VARCHAR(10),
                        MOE VARCHAR(10),
                        SLD DECIMAL(25,2),
                        DT_IMPORTACAO DATETIME,
                        FLAG_ERRO BIT,
                        MENSAGEM_ERRO VARCHAR(MAX)
                    )", conn, transaction))
                        {
                            await cmd.ExecuteNonQueryAsync();
                        }

                        using (var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, transaction))
                        {
                            bulk.DestinationTableName = tempTableName;
                            bulk.BatchSize = 1000;
                            bulk.BulkCopyTimeout = 600;

                            // Mapeamento exato das colunas
                            bulk.ColumnMappings.Add("C_ID_UPLOAD", "C_ID_UPLOAD");
                            bulk.ColumnMappings.Add("R_NOME_ARQ", "R_NOME_ARQ");
                            bulk.ColumnMappings.Add("D_BASE", "D_BASE");
                            bulk.ColumnMappings.Add("CD_EMP", "CD_EMP");
                            bulk.ColumnMappings.Add("CD_CONTA", "CD_CONTA");
                            bulk.ColumnMappings.Add("PRZ", "PRZ");
                            bulk.ColumnMappings.Add("MOE", "MOE");
                            bulk.ColumnMappings.Add("SLD", "SLD");
                            bulk.ColumnMappings.Add("DT_IMPORTACAO", "DT_IMPORTACAO");
                            bulk.ColumnMappings.Add("FLAG_ERRO", "FLAG_ERRO");
                            bulk.ColumnMappings.Add("MENSAGEM_ERRO", "MENSAGEM_ERRO");

                            await bulk.WriteToServerAsync(dt);
                            await AtualizarLogProcessamentoAsync(conn, lote, "LEITURA/PARSE CSV APP", "INFO", "Dados carregados na tabela temporária", transaction);
                            _logger.LogInformation($"{lote.codProc} -> Dados enviados para tabela temporária");
                        }

                        // INSERT com os nomes corretos das colunas
                        await ProcessarComando($@"
                                INSERT INTO dbo.STG_BALG (
                                    C_ID_UPLOAD, R_NOME_ARQ, D_BASE, CD_EMP, CD_CONTA, 
                                    PRZ, MOE, SLD, DT_IMPORTACAO, FLAG_ERRO, MENSAGEM_ERRO
                                )
                                SELECT 
                                    C_ID_UPLOAD, R_NOME_ARQ, D_BASE, CD_EMP, CD_CONTA, 
                                    PRZ, MOE, SLD, DT_IMPORTACAO, FLAG_ERRO, MENSAGEM_ERRO
                                FROM {tempTableName}", conn, transaction);

                        await ProcessarComando($"DROP TABLE IF EXISTS {tempTableName}", conn, transaction);

                        // Remove a tabela temporária
                        //using (var cmd = new SqlCommand($"DROP TABLE IF EXISTS {tempTableName}", conn, transaction))
                        //{
                        //    await cmd.ExecuteNonQueryAsync();
                        //}
                        _logger.LogInformation($"{lote.codProc} -> Tabela temporária {tempTableName} removida");

                        await AtualizarLogProcessamentoAsync(conn, lote, "CARGA STAGING", "INFO", "Dados inseridos na tabela STG_BALG", transaction);

                        _logger.LogInformation($"{lote.codProc} -> # 02 - Dados inseridos na tabela STAGING 'dbo.STG_BALG'");

                        await ProcessarDadosDoStaging(conn, lote);

                        await AtualizarLogProcessamentoAsync(conn, lote, "FINALIZAÇÃO", "SUCESSO", "Processamento concluído com sucesso", transaction);
                        _logger.LogInformation($"{lote.codProc} -> Status do upload atualizado");


                        transaction.Commit();

                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();

                        await AtualizarLogProcessamentoAsync(conn, lote, "ERRO", "ERRO - ROLLBACK", ex.Message, transaction);
                        _logger.LogError(ex, $"{lote.codProc} -> Erro durante processamento SQL");
                        throw;
                    }
                }


            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{lote.codProc} -> Erro ao processar SQL");
                throw;
            }
        }

        private static async Task ProcessarComando(string sqlComando, SqlConnection conn, SqlTransaction transaction)
        {
            if (transaction != null)
            {
                using (var cmd = new SqlCommand(sqlComando, conn, transaction))
                {
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            else
            {
                using (var cmd = new SqlCommand(sqlComando, conn))
                {
                    await cmd.ExecuteNonQueryAsync();
                }
            }

        }

        private async Task ProcessarDadosDoStaging(SqlConnection conn, LoteProcessamento lote)
        {
            //throw new NotImplementedException();

            _logger.LogInformation($"{lote.codProc} -> # 03 - (Implementar) Staging processado para  'dbo.T_PCONP_BASE_CONSL_ANLTCA'");

            return;
        }
        #endregion

        #region Controle de Upload (Status e Logs)

        private async Task<int> CriarControleDeUploadAsync(int idUpload, string usuario)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
                INSERT INTO T_CTRL_UPLOAD_BALG (C_ID_UPLOAD, R_NOME_ARQ, STATUS, DT_INICIO)
                OUTPUT INSERTED.C_ID_CTRL_UPLOAD
                VALUES (@C_ID_UPLOAD, @NOME, 'PROCESSANDO', GETDATE());
            ", conn);

            cmd.Parameters.AddWithValue("@NOME", usuario);
            cmd.Parameters.AddWithValue("@C_ID_UPLOAD", idUpload);

            var id = (int)await cmd.ExecuteScalarAsync();
            return id;
        }

        private async Task AtualizarControleDeUploadAsync(int idUpload, string status, string? mensagem = null)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
                UPDATE T_CTRL_UPLOAD_BALG
                SET STATUS = @STATUS,
                    MENSAGEM = @MSG,
                    DT_FIM = CASE WHEN @STATUS IN ('FINALIZADO', 'ERRO') THEN GETDATE() ELSE DT_FIM END
                WHERE C_ID_UPLOAD = @ID;
            ", conn);

            cmd.Parameters.AddWithValue("@STATUS", status);
            cmd.Parameters.AddWithValue("@MSG", mensagem ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ID", idUpload);

            await cmd.ExecuteNonQueryAsync();
        }

        #endregion

        public async Task<List<LoteProcessamento>> DividirEmLotesAsync(List<Arquivo> arquivos)
        {
            _logger.LogInformation($"Iniciando divisão em lotes. Total de arquivos: {arquivos.Count}");

            var lotes = new List<LoteProcessamento>();
            var tamanhoLote = 50;
            var idLote = (int)(DateTime.Now.Ticks % int.MaxValue);

            if (arquivos.Count <= tamanhoLote)
            {
                _logger.LogInformation("Criando lote único");
                lotes.Add(new LoteProcessamento
                {
                    IdLote = idLote,
                    Arquivos = arquivos
                });
            }
            else
            {
                _logger.LogInformation($"Dividindo em lotes de {tamanhoLote} arquivos");
                for (int i = 0; i < arquivos.Count; i += tamanhoLote)
                {
                    var arquivosLote = arquivos.Skip(i).Take(tamanhoLote).ToList();
                    lotes.Add(new LoteProcessamento
                    {
                        IdLote = idLote + i,
                        Arquivos = arquivosLote
                    });
                }
            }

            _logger.LogInformation($"Total de lotes criados: {lotes.Count}");
            return lotes;
        }

        public async Task<LoteProcessamento> ProcessarLoteAsync(LoteProcessamento lote)
        {
            _logger.LogInformation($"Iniciando processamento do lote {lote.IdLote} com {lote.Arquivos.Count} arquivos");

            lote.DataInicio = DateTime.Now;
            var erros = new List<string>();
            int arqProc = 0;
            lote.Status = "PROCESSANDO";
            int idUpload = await CriarControleDeUploadAsync(lote.IdLote, "PASTA_UPLOAD");
            await AtualizarControleDeUploadAsync(lote.IdLote, lote.Status);

            // Processa arquivos sequencialmente para evitar deadlocks
            foreach (var arquivo in lote.Arquivos)
            {
                try
                {
                    lote.codProc = arquivo.FileName;
                    lote.idArq = (int)(DateTime.Now.Ticks % int.MaxValue);

                    _logger.LogInformation($"{lote.codProc} -> {new string('-', 100)}");
                    _logger.LogInformation($"{lote.codProc} -> Processando arquivo {arquivo.FileName} no lote {lote.IdLote}");

                    await _statusService.EnviarStatus(lote, "PROCESSAMENTO", "PROCESSANDO", $"Processando arquivo {arquivo.FileName}");

                    await ProcessarArquivoComRetryAsync(arquivo, lote);

                    arqProc++;
                    arquivo.Status = StatusTipos.Concluido;
                    arquivo.FlagErro = false;
                    arquivo.MensagemErro = null;
                    _logger.LogInformation($"{lote.codProc} -> Arquivo {arquivo.FileName} processado com sucesso");
                }
                catch (Exception ex)
                {
                    arquivo.Status = StatusTipos.Erro;
                    arquivo.FlagErro = true;
                    arquivo.MensagemErro = $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
                    erros.Add($"Arquivo: {arquivo.FileName} - Erro: {ex.Message}");
                    _logger.LogError(ex, $"{lote.codProc} -> Erro ao processar arquivo {arquivo.FileName} no lote {lote.IdLote}");
                    // Envia status de erro detalhado via SignalR
                    await _statusService.EnviarStatus(lote, "ERRO", "ERRO", $"Erro ao processar arquivo {arquivo.FileName}: {ex.Message}\n{ex.StackTrace}");
                }
            }

            lote.Status = "FINALIZADO";
            lote.DataFim = DateTime.Now;
            lote.Mensagem = erros.Count == 0 ? "Lote processado com sucesso" : $"Lote finalizado com {erros.Count} erro(s):\n" + string.Join("\n", erros);
            await AtualizarControleDeUploadAsync(lote.IdLote, lote.Status, lote.Mensagem);

            _logger.LogInformation($"Lote {lote.IdLote} finalizado. {lote.Mensagem}");
            return lote;
        }

        private async Task ProcessarArquivoComRetryAsync(Arquivo arquivo, LoteProcessamento lote, int maxRetries = 3)
        {
            var retryCount = 0;
            var delay = TimeSpan.FromSeconds(2); // Delay inicial de 2 segundos

            while (true)
            {
                try
                {
                    await ProcessarArquivoIFormFileAsync(arquivo, lote);
                    return;
                }
                catch (SqlException ex) when (ex.Number == 1205 && retryCount < maxRetries) // 1205 é o código de deadlock
                {
                    retryCount++;
                    _logger.LogWarning($"{lote.codProc} -> Deadlock detectado ao processar {arquivo.FileName}. Tentativa {retryCount} de {maxRetries}");

                    if (retryCount >= maxRetries)
                    {
                        _logger.LogError($"{lote.codProc} -> Número máximo de tentativas excedido para {arquivo.FileName}");
                        throw;
                    }

                    // Espera exponencial: 2s, 4s, 8s...
                    await Task.Delay(delay);
                    delay = TimeSpan.FromSeconds(delay.TotalSeconds * 2);
                }
            }
        }
    }
}

// ========== Services\StorageService.cs ==========
namespace BalgImport.Services
{
    public interface IStorageService
    {
        Task<List<UploadBatch>> LoadBatches();
        Task SaveBatches(List<UploadBatch> batches);
        Task<UploadBatch?> GetBatchById(Guid id);
        Task SaveBatch(UploadBatch batch);
    }

    public class StorageService : IStorageService
    {
        private readonly string _storagePath;
        private readonly ILogger<StorageService> _logger;
        private static readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);

        public StorageService(ILogger<StorageService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _storagePath = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            Directory.CreateDirectory(_storagePath);
        }

        public async Task<List<UploadBatch>> LoadBatches()
        {
            var filePath = Path.Combine(_storagePath, "batches.json");
            if (!File.Exists(filePath))
                return new List<UploadBatch>();

            await _fileLock.WaitAsync();
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<List<UploadBatch>>(json) ?? new List<UploadBatch>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar lotes do arquivo");
                return new List<UploadBatch>();
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task SaveBatches(List<UploadBatch> batches)
        {
            var filePath = Path.Combine(_storagePath, "batches.json");
            await _fileLock.WaitAsync();
            try
            {
                var json = JsonSerializer.Serialize(batches, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar lotes no arquivo");
                throw;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<UploadBatch?> GetBatchById(Guid id)
        {
            var batches = await LoadBatches();
            return batches.FirstOrDefault(b => b.Id == id);
        }

        public async Task SaveBatch(UploadBatch batch)
        {
            var batches = await LoadBatches();
            var existingBatch = batches.FirstOrDefault(b => b.Id == batch.Id);
            
            if (existingBatch != null)
            {
                var index = batches.IndexOf(existingBatch);
                batches[index] = batch;
            }
            else
            {
                batches.Add(batch);
            }

            await SaveBatches(batches);
        }
    }
}

// ========== Services\TestDataGenerator.cs ==========
namespace BalgImport.Services
{
    public class TestDataGenerator
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<TestDataGenerator> _logger;
        private readonly string _pastaOrigem;
        private static readonly Random _random = new Random();
        private static readonly string[] _sociedades = { "2968", "2969", "2970" };
        private static readonly string[] _contas = {
            "11280008", "13185709", "19910002", "30330771", "30430107",
            "30915055", "30915103", "30915158", "49930502", "61170302", "61180000"
        };

        public TestDataGenerator(IWebHostEnvironment environment, ILogger<TestDataGenerator> logger)
        {
            _environment = environment;
            _logger = logger;
            _pastaOrigem = Path.Combine(_environment.ContentRootPath, "Arquivos");
            Directory.CreateDirectory(_pastaOrigem);
        }

        public async Task GerarArquivosTesteAsync(int quantidadeArquivos, int linhasPorArquivo)
        {
            _logger.LogInformation($"Iniciando geração de {quantidadeArquivos} arquivos com {linhasPorArquivo} linhas cada");
            var _pastaOrigem = Path.Combine("C:\\temp", "Arquivos");
            Directory.CreateDirectory(_pastaOrigem);

            for (int i = 1; i <= quantidadeArquivos; i++)
            {
                var sociedade = $"{i + 999}";
                var competencia = DateTime.Now.ToString("yyyyMM");
                var nomeArquivo = $"{sociedade}_BALG_{competencia}.CSV";
                
                var caminhoArquivo = Path.Combine(_pastaOrigem, nomeArquivo);

                await GerarArquivoCsvAsync(caminhoArquivo, linhasPorArquivo, sociedade);
                _logger.LogInformation($"Arquivo {i} de {quantidadeArquivos} gerado: {nomeArquivo}");
            }
        }

        private async Task GerarArquivoCsvAsync(string caminhoArquivo, int quantidadeLinhas, string sociedade)
        {
            var linhas = new List<string>();
            var competencia = DateTime.Now.ToString("MMyyyy");

            // Primeira linha - cabeçalho descritivo
            linhas.Add("Período/Exercício;Sociedade;ContaInterna;Prazo;Moeda;Saldo");
            
            // Segunda linha - cabeçalho técnico
            linhas.Add("0FISCPER;0COMPANY;CA_CINTER;CA_PRAZO;0CURKEY_GC;0CS_TRN_GC");
            var qtd = _random.Next(100, quantidadeLinhas);
            // Gera linhas de dados
            for (int i = 0; i < qtd; i++)
            {
                var linha = new List<string>
                {
                    competencia,                                    // Período
                    sociedade,                                      // Sociedade (usando a mesma do nome do arquivo)
                    _contas[_random.Next(_contas.Length)],          // Conta Interna
                    "1",                                           // Prazo (fixo em 1 como no exemplo)
                    "BRL",                                         // Moeda (fixo em BRL como no exemplo)
                    (_random.NextDouble() * 200000000 - 100000000).ToString("F2", CultureInfo.InvariantCulture) // Saldo
                };

                linhas.Add(string.Join(";", linha));
            }

            // Escreve o arquivo
            await File.WriteAllLinesAsync(caminhoArquivo, linhas, Encoding.UTF8);
        }
    }
}

// ========== Services\UploadService.cs ==========
namespace BalgImport.Services
{
    public interface IUploadService
    {
        Task<UploadBatch> IniciarNovoBatch(string usuarioId, string usuarioNome);
        Task<UploadStatus> ProcessarUpload(IFormFile file, Guid batchId);
        Task<UploadBatch> ObterStatusBatch(Guid batchId);
        Task<IEnumerable<UploadBatch>> ObterTodosBatches();
        Task RetomarUpload(Guid batchId);
        Task CancelarUpload(Guid batchId);
    }

    public class UploadService : IUploadService
    {
        private readonly ILogger<UploadService> _logger;
        private readonly IStorageService _storageService;
        private readonly List<UploadBatch> _batches = new List<UploadBatch>();
        private readonly IHubContext<Hubs.UploadHub> _hubContext;

        public UploadService(ILogger<UploadService> logger, IStorageService storageService, IHubContext<Hubs.UploadHub> hubContext)
        {
            _logger = logger;
            _storageService = storageService;
            _hubContext = hubContext;
        }

        private async Task NotifyStatusChange(UploadBatch batch)
        {
            await _hubContext.Clients.All.SendAsync("statusChanged", new
            {
                batchId = batch.Id,
                status = batch.Status,
                arquivosProcessados = batch.ArquivosProcessados,
                arquivosComErro = batch.ArquivosComErro,
                totalArquivos = batch.TotalArquivos,
                arquivos = batch.Arquivos.Select(a => new
                {
                    nomeArquivo = a.NomeArquivo,
                    status = a.Status,
                    mensagemErro = a.MensagemErro
                }).ToList()
            });
        }

        public async Task<UploadBatch> IniciarNovoBatch(string usuarioId, string usuarioNome)
        {
            var batch = new UploadBatch
            {
                UsuarioId = usuarioId,
                UsuarioNome = usuarioNome,
                Status = "PROCESSANDO",
                DataInicio = DateTime.Now
            };

            await _storageService.SaveBatch(batch);
            _batches.Add(batch);
            await NotifyStatusChange(batch);
            return batch;
        }

        public async Task<UploadStatus> ProcessarUpload(IFormFile file, Guid batchId)
        {
            var batch = _batches.FirstOrDefault(b => b.Id == batchId);
            if (batch == null)
                throw new Exception("Batch não encontrado");

            var uploadStatus = new UploadStatus
            {
                NomeArquivo = file.FileName,
                Tamanho = file.Length,
                Status = "PROCESSANDO",
                DataInicio = DateTime.Now
            };

            batch.Arquivos.Add(uploadStatus);
            batch.Status = "PROCESSANDO";
            await NotifyStatusChange(batch);

            try
            {
                // Processa o arquivo
                using var stream = file.OpenReadStream();
                using var sha256 = SHA256.Create();
                uploadStatus.Hash = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLower();
                uploadStatus.Status = "CONCLUIDO";
                uploadStatus.DataFim = DateTime.Now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar arquivo {FileName}", file.FileName);
                uploadStatus.Status = "ERRO";
                uploadStatus.MensagemErro = ex.Message;
                uploadStatus.DataFim = DateTime.Now;
            }

            await _storageService.SaveBatch(batch);
            await NotifyStatusChange(batch);
            return uploadStatus;
        }

        public async Task<UploadBatch> ObterStatusBatch(Guid batchId)
        {
            var batch = _batches.FirstOrDefault(b => b.Id == batchId);
            if (batch == null)
            {
                throw new Exception($"Lote {batchId} não encontrado");
            }
            return batch;
        }

        public async Task<IEnumerable<UploadBatch>> ObterTodosBatches()
        {
            return _batches.ToList();
        }

        public async Task RetomarUpload(Guid batchId)
        {
            var batch = _batches.FirstOrDefault(b => b.Id == batchId);
            if (batch != null)
            {
                foreach (var arquivo in batch.Arquivos.Where(a => a.Status == "ERRO"))
                {
                    arquivo.Status = "PENDENTE";
                    arquivo.MensagemErro = null;
                    arquivo.DataInicio = DateTime.Now;
                    arquivo.DataFim = null;
                }
                batch.Status = "PROCESSANDO";
                await _storageService.SaveBatch(batch);
                await NotifyStatusChange(batch);
            }
        }

        public async Task CancelarUpload(Guid batchId)
        {
            var batch = _batches.FirstOrDefault(b => b.Id == batchId);
            if (batch != null)
            {
                foreach (var arquivo in batch.Arquivos.Where(a => a.Status == "PENDENTE"))
                {
                    arquivo.Status = "CANCELADO";
                    arquivo.DataFim = DateTime.Now;
                }
                batch.Status = "CANCELADO";
                await _storageService.SaveBatch(batch);
                await NotifyStatusChange(batch);
            }
        }
    }
}

// ========== Controllers\DashboardController.cs ==========
namespace BalgImport.Controllers
{
    public class DashboardController : Controller
    {
        private readonly IUploadService _uploadService;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(IUploadService uploadService, ILogger<DashboardController> logger)
        {
            _uploadService = uploadService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var batches = await _uploadService.ObterTodosBatches();
                var viewModel = new DashboardViewModel
                {
                    Lotes = batches.Select(b => new LoteViewModel
                    {
                        Id = b.Id,
                        DataCriacao = b.DataCriacao,
                        DataInicio = b.DataInicio,
                        DataFim = b.DataFim,
                        UsuarioId = b.UsuarioId,
                        UsuarioNome = b.UsuarioNome,
                        Status = b.Status,
                        TotalArquivos = b.TotalArquivos,
                        ArquivosProcessados = b.ArquivosProcessados,
                        ArquivosComErro = b.ArquivosComErro,
                        MensagemErro = b.MensagemErro
                    }).ToList(),
                    Indicadores = new IndicadoresViewModel
                    {
                        TotalLotes = batches.Count(),
                        TotalArquivos = batches.Sum(b => b.TotalArquivos),
                        ArquivosProcessados = batches.Sum(b => b.ArquivosProcessados),
                        ArquivosComErro = batches.Sum(b => b.ArquivosComErro),
                        LotesEmAndamento = batches.Count(b => b.Status == "PROCESSANDO"),
                        LotesConcluidos = batches.Count(b => b.Status == "CONCLUIDO"),
                        LotesComErro = batches.Count(b => b.Status == "ERRO")
                    }
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar dashboard");
                return View(new DashboardViewModel());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetIndicadores()
        {
            try
            {
                var batches = await _uploadService.ObterTodosBatches();
                var indicadores = new
                {
                    totalLotes = batches.Count(),
                    totalArquivos = batches.Sum(b => b.TotalArquivos),
                    arquivosProcessados = batches.Sum(b => b.ArquivosProcessados),
                    arquivosComErro = batches.Sum(b => b.ArquivosComErro),
                    lotesEmAndamento = batches.Count(b => b.Status == "PROCESSANDO"),
                    lotesConcluidos = batches.Count(b => b.Status == "CONCLUIDO"),
                    lotesComErro = batches.Count(b => b.Status == "ERRO")
                };

                return Json(new { success = true, data = indicadores });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter indicadores");
                return Json(new { success = false, error = "Erro ao obter indicadores" });
            }
        }
    }
}

// ========== Controllers\HomeController.cs ==========
﻿

// ========== Controllers\ImportacaoApiController.cs ==========
namespace Projeto_BALG_Import.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImportacaoApiController : ControllerBase
    {
        private readonly IImportacaoService _importacaoService;
        private readonly IHubContext<ImportacaoHub> _hubContext;
        private readonly ILogger<ImportacaoApiController> _logger;
        private readonly StatusImportacaoService _statusService;
        private readonly TestDataGenerator _testDataGenerator;

        public ImportacaoApiController(
            IImportacaoService importacaoService,
            IHubContext<ImportacaoHub> hubContext,
            ILogger<ImportacaoApiController> logger, StatusImportacaoService statusService, TestDataGenerator testDataGenerator)
        {
            _importacaoService = importacaoService;
            _hubContext = hubContext;
            _logger = logger;
            this._statusService = statusService;
            _testDataGenerator = testDataGenerator;
        }

        [HttpGet("gerar-id-unico")]
        public IActionResult GerarIdUnico()
        {
            try
            {
                // Gera um ID único baseado no timestamp atual
                // Formato: yyMMddhhmmss + milissegundos (3 dígitos)
                // Mas limitado ao range do INT (2.147.483.647)
                var now = DateTime.Now;
                var year = now.Year.ToString().Substring(2, 2); // Últimos 2 dígitos do ano
                var month = now.Month.ToString().PadLeft(2, '0');
                var day = now.Day.ToString().PadLeft(2, '0');
                var hour = now.Hour.ToString().PadLeft(2, '0');
                var minute = now.Minute.ToString().PadLeft(2, '0');
                var second = now.Second.ToString().PadLeft(2, '0');
                var millisecond = now.Millisecond.ToString().PadLeft(3, '0');
                
                var timestamp = $"{year}{month}{day}{hour}{minute}{second}{millisecond}";
                var idUnico = long.Parse(timestamp);
                
                // Garante que o ID não exceda o limite do INT
                if (idUnico > int.MaxValue)
                {
                    // Se exceder, usa apenas os primeiros dígitos
                    idUnico = idUnico % int.MaxValue;
                }
                
                _logger.LogInformation($"ID único gerado: {idUnico}");
                
                return Ok(new { 
                    success = true, 
                    idUnico = idUnico,
                    timestamp = timestamp
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar ID único");
                return StatusCode(500, new { 
                    success = false, 
                    erro = "Erro ao gerar ID único",
                    detalhes = ex.Message 
                });
            }
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadArquivos(IFormFile[] arquivos, string idUpload, string idLote)
        {
            try
            {
                //await _testDataGenerator.GerarArquivosTesteAsync(2000, 5000);
                //var idUpload = (int)(DateTime.Now.Ticks % int.MaxValue);
                
                var lote = new LoteProcessamento
                {
                    Arquivos = arquivos.Length > 0 ? 
                    arquivos.Select(f => new Arquivo { FileName = f.FileName, FormFile = f }).ToList() : new List<Arquivo>(),
                };

                lote.Usuario = Request.Form["usuario"].ToString();

                if (string.IsNullOrEmpty(lote.Usuario))
                {
                    lote.Usuario = User.Identity?.Name;
                }
              
                int.TryParse(Request.Form["idUpload"], out int reqIdUpload);

                lote.idUpload = reqIdUpload;
                int.TryParse(Request.Form["idLote"], out int reqIdLote);

                lote.IdLote = reqIdLote;

                

                _logger.LogInformation($"Recebida requisição de upload com {arquivos.Length} arquivos");

                // Validação inicial
                if (arquivos == null || arquivos.Length == 0) return BadRequest(new { erro = "Nenhum arquivo enviado" });

                // Inicia o processamento em background
                await Task.Run(async () =>
                {
                    try
                    {
                        _logger.LogInformation("Iniciando divisão em lotes");

                        try
                        {
                            await _statusService.EnviarStatusInicial(lote);
                            // Processa o lote
                            await _importacaoService.ProcessarLoteAsync(lote);

                            await _statusService.EnviarStatusConcluido(lote);

                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Erro ao processar lote {lote.IdLote}");
                            await _statusService.EnviarStatusErro(lote, $"Erro ao processar lote {lote.IdLote}: {ex.Message}");
                        }

                       
                        _logger.LogInformation("Processamento finalizado com sucesso");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro durante o processamento dos lotes");
                        await _statusService.EnviarStatusErro(lote, $"Erro inesperado ao processar o lote {lote.IdLote}: {ex.Message}");
                    }
                });

                _logger.LogInformation("Upload iniciado com sucesso");
                return Ok(new { mensagem = "Upload iniciado com sucesso" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar upload");
                return StatusCode(500, new { erro = ex.Message });
            }
        }
    }

   

    public class StatusImportacao
    {
        public long IdUpload { get; set; }
        public long IdLote { get; set; }
        public int? IdArquivo { get; set; }
        public string NomeArquivo { get; set; }
        public string Etapa { get; set; }
        public string Status { get; set; }
        public string Mensagem { get; set; }
        public DateTime DataHora { get; set; }
        public int TotalArquivos { get; set; }
        public int ArquivosProcessados { get; set; }
        public string Usuario { get;  set; }
        public DateTime? DataFim { get;  set; }
        public DateTime DataInicio { get;  set; }
    }
}

// ========== Controllers\ImportacaoController.cs ==========
namespace Projeto_BALG_Import.Controllers
{
    public class ImportacaoController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}

// ========== Controllers\StatusImportacaoService.cs ==========
﻿using BalgImport.Hubs;

namespace Projeto_BALG_Import.Controllers
{
    public static class StatusEtapas
    {
        public const string Inicio = "INICIO";
        public const string Processando = "PROCESSANDO";
        public const string Finalizado = "FINALIZADO";
    }

    public static class StatusTipos
    {
        public const string Iniciado = "INICIADO";
        public const string Processando = "PROCESSANDO";
        public const string Concluido = "CONCLUIDO";
        public const string Erro = "ERRO";
        public const string Cancelado = "CANCELADO";
        public const string Pendente = "PENDENTE";
    }

    public class StatusImportacaoService
    {
        private readonly IHubContext<ImportacaoHub> _hubContext;

        public StatusImportacaoService(IHubContext<ImportacaoHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task EnviarStatus(LoteProcessamento lote,
            string etapa,
            string status,
            string mensagem)
        {
            await _hubContext.Clients.All.SendAsync("ReceberStatusImportacao", 
                new StatusImportacao
                {
                    IdUpload = lote.idUpload,
                    IdLote = lote.IdLote,
                    IdArquivo = lote.idArq,
                    NomeArquivo = lote.codProc,
                    Etapa = etapa,
                    Status = status,
                    Mensagem = mensagem,
                    DataHora = DateTime.Now,
                    DataInicio = lote.DataInicio,
                    DataFim = lote.DataFim,
                    TotalArquivos = lote.Arquivos?.Count ?? 0,
                    ArquivosProcessados = lote.ArquivosProcessados, 
                    Usuario = lote.Usuario

                });
        }

        public async Task EnviarStatusInicial(LoteProcessamento lote)
        {
            await EnviarStatus(
                lote,
                etapa: "INICIO",
                status: "INICIADO",
                mensagem: "Iniciando a importação dos arquivos"
            );
        }

        public async Task EnviarStatusProcessamento(LoteProcessamento lote)
        {
            await EnviarStatus(
                lote,
                etapa: "PROCESSANDO",
                status: "PROCESSANDO",
                mensagem: $"Processando arquivos ({lote.ArquivosProcessados}/{lote.Arquivos?.Count ?? 0})"
            );
        }

        public async Task EnviarStatusConcluido(LoteProcessamento lote)
        {
            await EnviarStatus(
                lote,
                etapa: "FINALIZADO",
                status: "CONCLUIDO",
                mensagem: "Importação concluída com sucesso"
            );
        }

        public async Task EnviarStatusErro(LoteProcessamento lote, string mensagemErro)
        {
            await EnviarStatus(
                lote,
                etapa: "FINALIZADO",
                status: "ERRO",
                mensagem: mensagemErro
            );
        }

        public async Task EnviarResumoUpload(object resumo)
        {
            await _hubContext.Clients.All.SendAsync("AtualizarResumo", resumo);
        }

        public async Task EnviarLoteAtual(object loteAtual)
        {
            await _hubContext.Clients.All.SendAsync("AtualizarLoteAtual", loteAtual);
        }

        public async Task EnviarHistoricoLote(object loteFinalizado)
        {
            await _hubContext.Clients.All.SendAsync("AdicionarHistoricoLote", loteFinalizado);
        }
    }
}

// ========== Controllers\TestDataApiController.cs ==========
namespace BalgImport.Controllers
{
    public class TestDataApiController : ControllerBase
    {
        private readonly TestDataGenerator _testDataGenerator;
        private readonly ILogger<TestDataApiController> _logger;

        public TestDataApiController(TestDataGenerator testDataGenerator, ILogger<TestDataApiController> logger)
        {
            _testDataGenerator = testDataGenerator;
            _logger = logger;
        }

        [HttpGet("gerar")]
        public async Task<IActionResult> GerarDadosTeste(int qtdArq, int qtdLinhas )
        {
            try
            {
                await _testDataGenerator.GerarArquivosTesteAsync(qtdArq, qtdLinhas);
                return Ok(new { mensagem = "Arquivos de teste gerados com sucesso" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar arquivos de teste");
                return StatusCode(500, new { erro = "Erro ao gerar arquivos de teste", detalhes = ex.Message });
            }
        }
    }

    public class GerarDadosTesteRequest
    {
        public int QuantidadeArquivos { get; set; }
        public int LinhasPorArquivo { get; set; }
    }
}

// ========== Controllers\UploadApiController_old.cs ==========
﻿using Microsoft.AspNetCore.Mvc;
namespace BalgImport.Controllers.Api
{
    [ApiController]
    [Route("api/upload")]
    public class UploadApiController : ControllerBase
    {
        private readonly IUploadService _uploadService;
        private readonly ILogger<UploadApiController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly TestDataGenerator _testDataGenerator;

        public UploadApiController(
            IUploadService uploadService,
            ILogger<UploadApiController> logger,
            IWebHostEnvironment environment,
            TestDataGenerator testDataGenerator)
        {
            _uploadService = uploadService;
            _logger = logger;
            _environment = environment;
            _testDataGenerator = testDataGenerator;
        }

        [HttpPost("startbatch")]
        public async Task<ActionResult<Guid>> IniciarNovoBatch()
        {
            try
            {
                var batchId = await _uploadService.IniciarNovoBatch();
                return Ok(batchId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao iniciar novo batch");
                return StatusCode(500, "Erro ao iniciar novo batch");
            }
        }

        [HttpPost("uploadfile")]
        public async Task<ActionResult<StatusUpload>> UploadFile(IFormFile file, Guid batchId)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest("Arquivo inválido");

                if (batchId == Guid.Empty)
                    return BadRequest("Batch ID inválido");

                var status = await _uploadService.ProcessarUpload(file, batchId);
                return Ok(status);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Erro ao processar upload: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar upload");
                return StatusCode(500, "Erro ao processar upload");
            }
        }

        [HttpPost("cancelbatch/{batchId:guid}")]
        public async Task<ActionResult> CancelarBatch(Guid batchId)
        {
            try
            {
                var batch = await _uploadService.ObterStatusBatch(batchId);
                if (batch == null)
                    return NotFound("Batch não encontrado");

                if (batch.Status == "CANCELADO")
                    return BadRequest("Batch já está cancelado");

                batch.Status = "CANCELADO";
                batch.DataFinalizacao = DateTime.Now;

                // Cancela todos os uploads pendentes
                foreach (var arquivo in batch.Arquivos.Where(a => a.Status == "PENDENTE" || a.Status == "UPLOADING"))
                {
                    arquivo.Status = "CANCELADO";
                    arquivo.DataFim = DateTime.Now;
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao cancelar batch");
                return StatusCode(500, "Erro ao cancelar batch");
            }
        }

        [HttpGet("getstatus/{batchId:guid}")]
        public async Task<ActionResult<UploadBatch>> GetBatchStatus(Guid batchId)
        {
            try
            {
                var batch = await _uploadService.ObterStatusBatch(batchId);
                if (batch == null)
                    return NotFound("Batch não encontrado");

                return Ok(batch);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter status do batch");
                return StatusCode(500, "Erro ao obter status do batch");
            }
        }

        [HttpGet("getfile/{fileName}")]
        public async Task<IActionResult> GetFile(string fileName)
        {
            try
            {
                var filePath = Path.Combine(_environment.ContentRootPath, "Uploads", fileName);
                if (!System.IO.File.Exists(filePath))
                    return NotFound("Arquivo não encontrado");

                var content = await System.IO.File.ReadAllTextAsync(filePath, Encoding.UTF8);
                return Ok(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao ler arquivo");
                return StatusCode(500, "Erro ao ler arquivo");
            }
        }

        [HttpGet("download/{fileName}")]
        public async Task<IActionResult> DownloadFile(string fileName)
        {
            try
            {
                var filePath = Path.Combine(_environment.ContentRootPath, "Uploads", fileName);
                if (!System.IO.File.Exists(filePath))
                    return NotFound("Arquivo não encontrado");

                var memory = new MemoryStream();
                using (var stream = new FileStream(filePath, FileMode.Open))
                {
                    await stream.CopyToAsync(memory);
                }
                memory.Position = 0;

                return File(memory, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao baixar arquivo");
                return StatusCode(500, "Erro ao baixar arquivo");
            }
        }

        [HttpGet("getallbatches")]
        public async Task<ActionResult<List<UploadBatch>>> GetAllBatches()
        {
            try
            {
                var batches = await _uploadService.ObterTodosBatches();
                return Ok(batches);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter lotes");
                return StatusCode(500, "Erro ao obter lotes");
            }
        }

        [HttpPost("gerararquivosteste")]
        public async Task<IActionResult> GerarArquivosTeste()
        {
            try
            {
                var arquivos = await _testDataGenerator.GerarArquivosTeste();
                return Ok(new { arquivosGerados = arquivos });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar arquivos de teste");
                return StatusCode(500, "Erro ao gerar arquivos de teste");
            }
        }
    }
}

// ========== Controllers\UploadController.cs ==========
﻿using BalgImport.Hubs;
namespace BalgImport.Controllers
{
    public class UploadController : Controller
    {
        private readonly IUploadService _uploadService;
        private readonly IStorageService _storageService;
        private readonly IHubContext<UploadHub> _hubContext;

        public UploadController(
            IUploadService uploadService,
            IStorageService storageService,
            IHubContext<UploadHub> hubContext)
        {
            _uploadService = uploadService;
            _storageService = storageService;
            _hubContext = hubContext;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> TodosBatches()
        {
            try
            {
                var batches = await _uploadService.ObterTodosBatches();
                return Json(new { success = true, batches });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> IniciarNovoBatch()
        {
            try
            {
                var batch = await _uploadService.IniciarNovoBatch("1", "Usuário Teste");
                
                // Notifica os clientes sobre o novo lote
                await _hubContext.Clients.All.SendAsync("statusChanged", new
                {
                    batchId = batch.Id,
                    status = batch.Status,
                    arquivosProcessados = 0,
                    totalArquivos = 0
                });

                return Json(new { success = true, batchId = batch.Id });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ProcessarUpload(IFormFile file, [FromForm] string batchId)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return Json(new { success = false, error = "Nenhum arquivo enviado" });
                }

                if (string.IsNullOrEmpty(batchId) || !Guid.TryParse(batchId, out Guid batchIdGuid))
                {
                    return Json(new { success = false, error = "ID do lote inválido" });
                }

                // Processa o arquivo
                var status = await _uploadService.ProcessarUpload(file, batchIdGuid);

                // Notifica os clientes sobre a mudança de status
                await _hubContext.Clients.All.SendAsync("statusChanged", new
                {
                    batchId = batchIdGuid,
                    status = status.Status,
                    arquivosProcessados = 1,
                    totalArquivos = 1
                });

                return Json(new { success = true, status });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObterStatusBatch([FromQuery] string batchId)
        {
            try
            {
                if (string.IsNullOrEmpty(batchId) || !Guid.TryParse(batchId, out Guid batchIdGuid))
                {
                    return Json(new { success = false, error = "ID do lote inválido" });
                }

                var batch = await _uploadService.ObterStatusBatch(batchIdGuid);
                if (batch == null)
                    return Json(new { success = false, error = "Batch não encontrado" });

                return Json(new { success = true, batch });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RetomarUpload(Guid batchId)
        {
            try
            {
                await _uploadService.RetomarUpload(batchId);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CancelarUpload(Guid batchId)
        {
            try
            {
                await _uploadService.CancelarUpload(batchId);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }
    }
}

// ========== Controllers\UploadController_old.cs ==========
﻿using Microsoft.AspNetCore.Mvc;
namespace BalgImport.Controllers
{
    public class UploadController : Controller
    {
        private readonly IUploadService _uploadService;
        private readonly ILogger<UploadController> _logger;

        public UploadController(IUploadService uploadService, ILogger<UploadController> logger)
        {
            _uploadService = uploadService;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UploadFiles(IFormFileCollection files)
        {
            try
            {
                var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var usuarioNome = User.FindFirst(ClaimTypes.Name)?.Value;

                // Inicia um novo batch
                var batch = await _uploadService.IniciarNovoBatch(usuarioId, usuarioNome);

                // Processa cada arquivo
                foreach (var file in files)
                {
                    await _uploadService.ProcessarUpload(file, batch.Id);
                }

                return Json(new { success = true, batchId = batch.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao fazer upload dos arquivos");
                return Json(new { success = false, error = "Erro ao fazer upload dos arquivos" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetStatus(Guid batchId)
        {
            try
            {
                var batch = await _uploadService.ObterStatusBatch(batchId);
                if (batch == null)
                {
                    return Json(new { success = false, error = "Batch não encontrado" });
                }

                // Formata o status para o formato esperado pelo JavaScript
                var status = new
                {
                    batchId = batch.Id,
                    batchStatus = batch.Status.ToString(),
                    totalFiles = batch.Arquivos.Count,
                    processedFiles = batch.Arquivos.Count(f => f.Status == FileStatus.Processado || f.Status == FileStatus.Erro),
                    files = batch.Arquivos.Select(f => new
                    {
                        fileName = f.NomeArquivo,
                        status = f.Status.ToString(),
                        error = f.MensagemErro
                    })
                };

                return Json(new { success = true, ...status });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter status do batch {BatchId}", batchId);
                return Json(new { success = false, error = "Erro ao obter status do batch" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetBatches()
        {
            try
            {
                var batches = await _uploadService.ObterTodosBatches();
                return Json(new { success = true, batches });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter todos os batches");
                return Json(new { success = false, error = "Erro ao obter batches" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RetomarUpload(Guid batchId)
        {
            try
            {
                await _uploadService.RetomarUpload(batchId);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao retomar upload do batch {BatchId}", batchId);
                return Json(new { success = false, error = "Erro ao retomar upload" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CancelarUpload(Guid batchId)
        {
            try
            {
                await _uploadService.CancelarUpload(batchId);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao cancelar upload do batch {BatchId}", batchId);
                return Json(new { success = false, error = "Erro ao cancelar upload" });
            }
        }
    }
}

