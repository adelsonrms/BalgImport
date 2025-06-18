using Microsoft.AspNetCore.Mvc;
using BalgImport.Services;
using BalgImport.Models;
using System.Security.Claims;
using System.Threading.Tasks;

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
