using BalgImport.Services;
using BalgImport.Models;
using BalgImport.Services;

using Microsoft.AspNetCore.Mvc;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace BalgImport.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

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

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        public async Task<IActionResult> UploadFiles(List<IFormFile> files)
        {
            if (files == null || !files.Any())
            {
                TempData["Mensagem"] = "Nenhum arquivo selecionado.";
                return RedirectToAction("Index");
            }

            try
            {
                var batchId = await _uploadService.IniciarNovoBatch();

                foreach (var file in files)
                {
                    await _uploadService.ProcessarUpload(file, batchId);
                }

                TempData["Mensagem"] = "Arquivos enviados com sucesso.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar uploads");
                TempData["Mensagem"] = "Erro ao processar uploads: " + ex.Message;
                return RedirectToAction("Index");
            }
        }
    }
}

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
                    Guid.TryParse(Request.Form["batchId"], out batchId);

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

//namespace BalgImport.Services
//{
//    public interface IUploadService
//    {
//        Task<Guid> IniciarNovoBatch();
//        Task<StatusUpload> ProcessarUpload(IFormFile arquivo, Guid batchId);
//        Task<UploadBatch?> ObterStatusBatch(Guid batchId);
//        Task<List<UploadBatch>> ObterTodosBatches();
//        Task<bool> RetomarUpload(string nomeArquivo, Guid batchId);
//        Task<bool> CancelarUpload(string nomeArquivo, Guid batchId);
//    }

//    public class UploadService : IUploadService
//    {
//        private static readonly ConcurrentDictionary<Guid, UploadBatch> _batches = new();
//        private static readonly ConcurrentDictionary<string, StatusUpload> _uploads = new();
//        private readonly ILogger<UploadService> _logger;
//        private readonly string _uploadPath;

//        public UploadService(ILogger<UploadService> logger, IConfiguration configuration)
//        {
//            _logger = logger;
//            _uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
//            Directory.CreateDirectory(_uploadPath);
//        }

//        public Task<Guid> IniciarNovoBatch()
//        {
//            var batch = new UploadBatch();
//            _batches.TryAdd(batch.Id, batch);
//            return Task.FromResult(batch.Id);
//        }

//        public async Task<StatusUpload> ProcessarUpload(IFormFile arquivo, Guid batchId)
//        {
//            if (!_batches.TryGetValue(batchId, out var batch))
//                throw new ArgumentException("Batch não encontrado");

//            var fileName = Path.GetFileName(arquivo.FileName);
//            var filePath = Path.Combine(_uploadPath, fileName);

//            var upload = new StatusUpload
//            {
//                NomeArquivo = fileName,
//                Status = "PENDENTE",
//                BatchId = batchId,
//                CaminhoArquivo = filePath,
//                TamanhoArquivo = arquivo.Length,
//                DataInicio = DateTime.Now
//            };

//            _uploads.TryAdd(fileName, upload);
//            batch.Arquivos.Add(upload);
//            batch.TotalArquivos++;

//            // Inicia o processamento assíncrono
//            _ = Task.Run(async () =>
//            {
//                try
//                {
//                    upload.Status = "UPLOADING";

//                    // Copia o arquivo para um stream temporário e salva
//                    using (var tempStream = new MemoryStream())
//                    {
//                        await arquivo.CopyToAsync(tempStream);
//                        tempStream.Position = 0;

//                        using (var fileStream = new FileStream(filePath, FileMode.Create))
//                        {
//                            await tempStream.CopyToAsync(fileStream);
//                        }
//                    }

//                    // Simula progresso do upload
//                    for (int i = 0; i <= 100; i += 10)
//                    {
//                        upload.Progresso = i;
//                        await Task.Delay(200); // 200ms entre cada atualização
//                    }

//                    // Calcular hash do arquivo
//                    upload.HashArquivo = await CalcularHashArquivo(filePath);

//                    upload.Status = "PROCESSANDO";
//                    // Simula processamento
//                    for (int i = 0; i <= 100; i += 20)
//                    {
//                        upload.Progresso = i;
//                        await Task.Delay(500); // 500ms entre cada atualização
//                    }

//                    upload.Status = "FINALIZADO";
//                    upload.DataFim = DateTime.Now;
//                    upload.Progresso = 100;

//                    batch.ArquivosProcessados++;
//                    if (batch.ArquivosProcessados + batch.ArquivosComErro == batch.TotalArquivos)
//                    {
//                        batch.Status = "FINALIZADO";
//                        batch.DataFinalizacao = DateTime.Now;
//                    }
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogError(ex, $"Erro ao processar arquivo {fileName}");
//                    upload.Status = "ERRO";
//                    upload.Mensagem = ex.Message;
//                    upload.DataFim = DateTime.Now;
//                    batch.ArquivosComErro++;
//                }
//            });

//            return upload;
//        }

//        public Task<UploadBatch?> ObterStatusBatch(Guid batchId)
//        {
//            return Task.FromResult(_batches.TryGetValue(batchId, out var batch) ? batch : null);
//        }

//        public Task<List<UploadBatch>> ObterTodosBatches()
//        {
//            return Task.FromResult(_batches.Values.OrderByDescending(x => x.DataCriacao).ToList());
//        }

//        public Task<bool> RetomarUpload(string nomeArquivo, Guid batchId)
//        {
//            if (!_uploads.TryGetValue(nomeArquivo, out var upload) || upload.BatchId != batchId)
//                return Task.FromResult(false);

//            upload.Status = "PENDENTE";
//            upload.Tentativas++;
//            upload.DataInicio = DateTime.Now;
//            upload.DataFim = null;
//            upload.Progresso = 0;

//            // Aqui você implementaria a lógica de retomada do upload
//            return Task.FromResult(true);
//        }

//        public Task<bool> CancelarUpload(string nomeArquivo, Guid batchId)
//        {
//            if (!_uploads.TryGetValue(nomeArquivo, out var upload) || upload.BatchId != batchId)
//                return Task.FromResult(false);

//            upload.Status = "CANCELADO";
//            upload.DataFim = DateTime.Now;
//            return Task.FromResult(true);
//        }

//        private async Task<string> CalcularHashArquivo(string filePath)
//        {
//            using var md5 = MD5.Create();
//            using var stream = File.OpenRead(filePath);
//            var hash = await md5.ComputeHashAsync(stream);
//            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
//        }
//    }
//}

namespace BalgImport.Services
{
    public class TestDataGenerator
    {
        private static readonly Random _random = new Random();
        private static readonly string[] _moedas = { "BRL", "USD", "EUR" };
        private static readonly int[] _prazos = { 1, 30, 60, 90, 180, 360 };
        private readonly string[] _sociedades = { "2968", "2969", "2970" };
        private readonly string[] _contas = {
            "11280008", "13185709", "19910002", "30330771", "30430107",
            "30915055", "30915103", "30915158", "49930502", "61170302", "61180000"
        };

        public static string GerarArquivoTeste(int codigoEmpresa, string layout, DateTime competencia, int numLinhas)
        {
            var sb = new StringBuilder();

            // Cabeçalho
            sb.AppendLine("Período/Exercício;Sociedade;ContaInterna;Prazo;Moeda;Saldo");

            // Linha de layout
            sb.AppendLine("0FISCPER;0COMPANY;CA_CINTER;CA_PRAZO;0CURKEY_GC;0CS_TRN_GC");

            // Gera linhas de dados
            for (int i = 0; i < numLinhas; i++)
            {
                var linha = GerarLinha(codigoEmpresa, competencia);
                sb.AppendLine(linha);
            }

            return sb.ToString();
        }

        private static string GerarLinha(int codigoEmpresa, DateTime competencia)
        {
            var periodo = competencia.ToString("MMyyyy");
            var contaInterna = GerarContaInterna();
            var prazo = _prazos[_random.Next(_prazos.Length)];
            var moeda = _moedas[_random.Next(_moedas.Length)];
            var saldo = GerarSaldo();

            return $"{periodo};{codigoEmpresa};{contaInterna};{prazo};{moeda};{saldo}";
        }

        private static string GerarContaInterna()
        {
            // Gera contas no formato 8 dígitos
            return _random.Next(10000000, 99999999).ToString();
        }

        private static string GerarSaldo()
        {
            // Gera saldos entre -1.000.000.000 e 1.000.000.000 com 2 casas decimais
            var saldo = _random.NextDouble() * 2000000000 - 1000000000;
            return saldo.ToString("F2");
        }

        public static List<(string NomeArquivo, string Conteudo)> GerarLoteTeste(int numArquivos)
        {
            var arquivos = new List<(string NomeArquivo, string Conteudo)>();
            var layouts = new[] { "BALG", "PART" };
            var empresas = new[] { 1001, 2002, 3003, 4004, 5005 };
            var competencias = new[]
            {
                new DateTime(2024, 1, 1),
                new DateTime(2024, 2, 1),
                new DateTime(2024, 3, 1),
                new DateTime(2024, 4, 1),
                new DateTime(2024, 5, 1)
            };

            for (int i = 0; i < numArquivos; i++)
            {
                var codigoEmpresa = empresas[_random.Next(empresas.Length)];
                var layout = layouts[_random.Next(layouts.Length)];
                var competencia = competencias[_random.Next(competencias.Length)];
                var numLinhas = _random.Next(100, 3001);

                var nomeArquivo = $"{codigoEmpresa}_{layout}_{competencia:yyyyMM}.CSV";
                var conteudo = GerarArquivoTeste(codigoEmpresa, layout, competencia, numLinhas);

                arquivos.Add((nomeArquivo, conteudo));
            }

            return arquivos;
        }

        public async Task<List<string>> GerarArquivosTeste()
        {
            var arquivos = new List<string>();
            var diretorio = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", "Testes");
            Directory.CreateDirectory(diretorio);

            for (int i = 0; i < 50; i++)
            {
                var nomeArquivo = $"2002_PART_{DateTime.Now:yyyyMM}.CSV";
                var conteudo = GerarConteudoArquivo();
                var caminho = Path.Combine(diretorio, nomeArquivo);

                await File.WriteAllTextAsync(caminho, conteudo);
                arquivos.Add(nomeArquivo);
            }

            return arquivos;
        }

        private string GerarConteudoArquivo()
        {
            var sb = new StringBuilder();

            // Cabeçalho
            sb.AppendLine("Período/Exercício;Sociedade;ContaInterna;Prazo;Moeda;Saldo");
            sb.AppendLine("0FISCPER;0COMPANY;CA_CINTER;CA_PRAZO;0CURKEY_GC;0CS_TRN_GC");

            // Linhas de dados
            var numLinhas = _random.Next(100, 1000);
            for (int i = 0; i < numLinhas; i++)
            {
                var linha = new[]
                {
                    $"02{DateTime.Now:yyyy}",
                    _sociedades[_random.Next(_sociedades.Length)],
                    _contas[_random.Next(_contas.Length)],
                    "1",
                    "BRL",
                    (_random.NextDouble() * 1000000 - 500000).ToString("F2")
                };
                sb.AppendLine(string.Join(";", linha));
            }

            return sb.ToString();
        }
    }
}

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllersWithViews();
        builder.Services.AddScoped<IUploadService, UploadService>();
        builder.Services.AddSingleton<TestDataGenerator>();

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

        app.UseAuthorization();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        app.MapControllerRoute(
            name: "api",
            pattern: "api/{controller}/{action}/{id?}",
            defaults: new { controller = "Upload", action = "Index" });

        app.Run();
    }
}