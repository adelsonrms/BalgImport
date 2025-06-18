using BalgImport.Hubs;
using BalgImport.Models;
using BalgImport.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

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
