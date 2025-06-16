using Microsoft.AspNetCore.Mvc;

using System.Collections.Concurrent;

namespace SeuProjeto.Controllers.Api
{
    [Route("api/upload")]
    [ApiController]
    public class UploadApiController : ControllerBase
    {
        private static readonly ConcurrentDictionary<string, StatusUpload> Uploads = new();

        [HttpPost("uploadfile")]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Arquivo inválido.");

            var fileName = Path.GetFileName(file.FileName);

            // Salvar fisicamente
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var upload = new StatusUpload
            {
                NomeArquivo = fileName,
                Status = "UPLOADING",
                Mensagem = "",
                Progresso = 0
            };

            Uploads.TryAdd(fileName, upload);

            // Simula processamento controlado
            _ = Task.Run(async () =>
            {
                try
                {
                    var rand = new Random();
                    upload.DataInicio = DateTime.Now;

                    // Simula upload
                    for (int i = 1; i <= 30; i++)
                    {
                        upload.Progresso = i * 2;
                        upload.Status = "UPLOADING";
                        await Task.Delay(rand.Next(200, 500)); // Upload delay variável
                    }

                    upload.Status = "PROCESSANDO_SQL";

                    // Simula processamento SQL
                    for (int i = 31; i <= 100; i++)
                    {
                        upload.Progresso = i;
                        await Task.Delay(rand.Next(300, 800)); // Processamento delay variável
                    }

                    upload.Status = "FINALIZADO";
                    upload.DataFim = DateTime.Now;
                    upload.Progresso = 100;
                }
                catch (Exception ex)
                {
                    upload.Status = "ERRO";
                    upload.Mensagem = ex.Message;
                    upload.DataFim = DateTime.Now;
                }
            });

            return Ok(new { status = "OK", arquivo = fileName });
        }

        [HttpGet("getstatus")]
        public IActionResult GetStatus()
        {
            return Ok(Uploads.Values.OrderByDescending(x => x.DataInicio).ToList());
        }
    }
}
