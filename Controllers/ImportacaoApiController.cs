using BalgImport.Hubs;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

using Projeto_BALG_Import.Services;

namespace Projeto_BALG_Import.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImportacaoApiController : ControllerBase
    {
        private readonly IImportacaoService _importacaoService;
        private readonly IHubContext<ImportacaoHub> _hubContext;
        private readonly ILogger<ImportacaoApiController> _logger;

        public ImportacaoApiController(
            IImportacaoService importacaoService,
            IHubContext<ImportacaoHub> hubContext,
            ILogger<ImportacaoApiController> logger)
        {
            _importacaoService = importacaoService;
            _hubContext = hubContext;
            _logger = logger;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadArquivos(IFormFile[] arquivos)
        {
            try
            {
                var idUpload = (int)(DateTime.Now.Ticks % int.MaxValue);

                _logger.LogInformation($"Recebida requisição de upload com {arquivos.Length} arquivos");

                // Validação inicial
                if (arquivos == null || arquivos.Length == 0) return BadRequest(new { erro = "Nenhum arquivo enviado" });

                // Inicia o processamento em background
                await Task.Run(async () =>
                {
                    try
                    {
                        _logger.LogInformation("Iniciando divisão em lotes");
                        var lotes = await _importacaoService.DividirEmLotesAsync(arquivos.ToList());
                        _logger.LogInformation($"Total de lotes criados: {lotes.Count}");

                        foreach (var lote in lotes)
                        {
                            try
                            {
                                lote.idUpload = idUpload;
                                _logger.LogInformation($"Processando lote {lote.IdLote} com {lote.Arquivos.Count} arquivos");

                                // Envia status inicial do lote
                                await _hubContext.Clients.All.SendAsync("ReceberStatusImportacao", 
                                new
                                {
                                    idUpload = idUpload,
                                    idLote = lote.IdLote,
                                    idArquivo = lote.idArq,
                                    nomeArquivo = lote.codProc,
                                    etapa = "INICIO",
                                    status = "INICIADO",
                                    mensagem = "Iniciando a importação dos arquivos",
                                    dataHora = DateTime.Now,
                                    totalArquivos = lote.Arquivos?.Count ?? 0,
                                    arquivosProcessados = lote.Arquivos?.Count ?? 0
                                });

                                // Processa o lote
                                await _importacaoService.ProcessarLoteAsync(lote);


                                await _hubContext.Clients.All.SendAsync("ReceberStatusImportacao", new
                                {
                                    idUpload = idUpload,
                                    idLote = lote.IdLote,
                                    idArquivo = lote.idArq,
                                    nomeArquivo = lote.codProc,
                                    etapa = "FINALIZAÇÃO",
                                    status = "FINALIZADO",
                                    mensagem = "Todos os arquivos do lote foram processados com sucesso",
                                    dataHora = DateTime.Now,
                                    totalArquivos = lote.Arquivos?.Count ?? 0,
                                    arquivosProcessados = lote.Arquivos?.Count ?? 0
                                });
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Erro ao processar lote {lote.IdLote}");

                               

                                await _hubContext.Clients.All.SendAsync("ReceberStatusImportacao", new
                                {
                                    idUpload = idUpload,
                                    idLote = lote.IdLote,
                                    idArquivo = lote.idArq,
                                    nomeArquivo = lote.codProc,
                                    etapa = "FINALIZAÇÃO",
                                    status = "ERRO",
                                    mensagem = $"Erro no lote {lote.IdLote}: {ex.Message}",
                                    dataHora = DateTime.Now,
                                    totalArquivos = lote.Arquivos?.Count ?? 0,
                                    arquivosProcessados = lote.Arquivos?.Count ?? 0
                                });
                            }
                        }
                        _logger.LogInformation("Processamento finalizado com sucesso");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro durante o processamento dos lotes");

                        await _hubContext.Clients.All.SendAsync("ReceberStatusImportacao", new
                        {
                            NomeArquivo = "Processamento",
                            Status = "ERRO",
                            DataHora = DateTime.Now,
                            Mensagem = $"Erro geral: {ex.Message}"
                        });
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
}