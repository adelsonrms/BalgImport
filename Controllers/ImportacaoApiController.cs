using BalgImport.Hubs;
using BalgImport.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

using Projeto_BALG_Import.Services;
using BalgImport.Services;

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