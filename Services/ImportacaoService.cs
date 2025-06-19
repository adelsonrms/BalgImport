using BalgImport.Hubs;

using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;

using Projeto_BALG_Import.Controllers;

using System.Data;
using System.Globalization;

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
