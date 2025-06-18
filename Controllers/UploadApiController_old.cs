using Microsoft.AspNetCore.Mvc;
using BalgImport.Models;
using BalgImport.Services;
using System.Text;

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
