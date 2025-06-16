using Microsoft.AspNetCore.Mvc;

using SeuProjeto.Models;

namespace SeuProjeto.Controllers
{
    public class UploadController : Controller
    {
        private static readonly List<StatusUpload> Uploads = new();

        public IActionResult Index()
        {
            return View(Uploads.OrderByDescending(x => x.DataInicio).ToList());
        }

        [HttpPost]
        public async Task<IActionResult> UploadFiles(List<IFormFile> files)
        {
            if (files == null || !files.Any())
            {
                TempData["Mensagem"] = "Nenhum arquivo selecionado.";
                return RedirectToAction("Index");
            }

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file.FileName);
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", fileName);

                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var upload = new StatusUpload
                {
                    NomeArquivo = fileName,
                    Status = "Processando"
                };

                Uploads.Add(upload);

                // Simula processamento async
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(3000); // Simula tempo de processamento
                        upload.Status = "Finalizado";
                        upload.DataFim = DateTime.Now;
                    }
                    catch (Exception ex)
                    {
                        upload.Status = "Erro";
                        upload.Mensagem = ex.Message;
                        upload.DataFim = DateTime.Now;
                    }
                });
            }

            TempData["Mensagem"] = "Arquivos enviados com sucesso.";
            return RedirectToAction("Index");
        }
    }
}

namespace SeuProjeto.Models
{
    public class StatusUpload
    {
        public string NomeArquivo { get; set; }
        public string Status { get; set; }
        public string Mensagem { get; set; }
        public DateTime DataInicio { get; set; } = DateTime.Now;
        public DateTime? DataFim { get; set; }
    }
}


namespace SeuProjeto.Controllers.Api
{

    public class StatusUpload
    {
        public string NomeArquivo { get; set; }
        public string Status { get; set; } // UPLOADING, PROCESSANDO_SQL, FINALIZADO, ERRO
        public string Mensagem { get; set; }
        public int Progresso { get; set; } = 0; // 0 a 100
        public DateTime DataInicio { get; set; } = DateTime.Now;
        public DateTime? DataFim { get; set; }
    }

}
