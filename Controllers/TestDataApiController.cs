using Microsoft.AspNetCore.Mvc;
using BalgImport.Services;
using System.Threading.Tasks;

namespace BalgImport.Controllers
{
    public class TestDataApiController : ControllerBase
    {
        private readonly TestDataGenerator _testDataGenerator;
        private readonly ILogger<TestDataApiController> _logger;

        public TestDataApiController(TestDataGenerator testDataGenerator, ILogger<TestDataApiController> logger)
        {
            _testDataGenerator = testDataGenerator;
            _logger = logger;
        }

        [HttpGet("gerar")]
        public async Task<IActionResult> GerarDadosTeste(int qtdArq, int qtdLinhas )
        {
            try
            {
                await _testDataGenerator.GerarArquivosTesteAsync(qtdArq, qtdLinhas);
                return Ok(new { mensagem = "Arquivos de teste gerados com sucesso" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar arquivos de teste");
                return StatusCode(500, new { erro = "Erro ao gerar arquivos de teste", detalhes = ex.Message });
            }
        }
    }

    public class GerarDadosTesteRequest
    {
        public int QuantidadeArquivos { get; set; }
        public int LinhasPorArquivo { get; set; }
    }
} 