using Microsoft.AspNetCore.Mvc;
using BalgImport.Services;
using BalgImport.Models;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

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