using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace BalgImport.Services
{
    public class TestDataGenerator
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<TestDataGenerator> _logger;
        private readonly string _pastaOrigem;
        private static readonly Random _random = new Random();
        private static readonly string[] _sociedades = { "2968", "2969", "2970" };
        private static readonly string[] _contas = {
            "11280008", "13185709", "19910002", "30330771", "30430107",
            "30915055", "30915103", "30915158", "49930502", "61170302", "61180000"
        };

        public TestDataGenerator(IWebHostEnvironment environment, ILogger<TestDataGenerator> logger)
        {
            _environment = environment;
            _logger = logger;
            _pastaOrigem = Path.Combine(_environment.ContentRootPath, "Arquivos");
            Directory.CreateDirectory(_pastaOrigem);
        }

        public async Task GerarArquivosTesteAsync(int quantidadeArquivos, int linhasPorArquivo)
        {
            _logger.LogInformation($"Iniciando geração de {quantidadeArquivos} arquivos com {linhasPorArquivo} linhas cada");
            var _pastaOrigem = Path.Combine("C:\\temp", "Arquivos");
            Directory.CreateDirectory(_pastaOrigem);

            for (int i = 1; i <= quantidadeArquivos; i++)
            {
                var sociedade = $"{i + 999}";
                var competencia = DateTime.Now.ToString("yyyyMM");
                var nomeArquivo = $"{sociedade}_BALG_{competencia}.CSV";
                
                var caminhoArquivo = Path.Combine(_pastaOrigem, nomeArquivo);

                await GerarArquivoCsvAsync(caminhoArquivo, linhasPorArquivo, sociedade);
                _logger.LogInformation($"Arquivo {i} de {quantidadeArquivos} gerado: {nomeArquivo}");
            }
        }

        private async Task GerarArquivoCsvAsync(string caminhoArquivo, int quantidadeLinhas, string sociedade)
        {
            var linhas = new List<string>();
            var competencia = DateTime.Now.ToString("MMyyyy");

            // Primeira linha - cabeçalho descritivo
            linhas.Add("Período/Exercício;Sociedade;ContaInterna;Prazo;Moeda;Saldo");
            
            // Segunda linha - cabeçalho técnico
            linhas.Add("0FISCPER;0COMPANY;CA_CINTER;CA_PRAZO;0CURKEY_GC;0CS_TRN_GC");
            var qtd = _random.Next(100, quantidadeLinhas);
            // Gera linhas de dados
            for (int i = 0; i < qtd; i++)
            {
                var linha = new List<string>
                {
                    competencia,                                    // Período
                    sociedade,                                      // Sociedade (usando a mesma do nome do arquivo)
                    _contas[_random.Next(_contas.Length)],          // Conta Interna
                    "1",                                           // Prazo (fixo em 1 como no exemplo)
                    "BRL",                                         // Moeda (fixo em BRL como no exemplo)
                    (_random.NextDouble() * 200000000 - 100000000).ToString("F2", CultureInfo.InvariantCulture) // Saldo
                };

                linhas.Add(string.Join(";", linha));
            }

            // Escreve o arquivo
            await File.WriteAllLinesAsync(caminhoArquivo, linhas, Encoding.UTF8);
        }
    }
}
