using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using BalgImport.Models;
using Microsoft.AspNetCore.Http;

namespace BalgImport.Services
{
    public interface IUploadService
    {
        Task<Guid> IniciarNovoBatch();
        Task<StatusUpload> ProcessarUpload(IFormFile arquivo, Guid batchId);
        Task<UploadBatch?> ObterStatusBatch(Guid batchId);
        Task<List<UploadBatch>> ObterTodosBatches();
        Task<bool> RetomarUpload(string nomeArquivo, Guid batchId);
        Task<bool> CancelarUpload(string nomeArquivo, Guid batchId);
    }

    public class UploadService : IUploadService
    {
        private static readonly ConcurrentDictionary<Guid, UploadBatch> _batches = new();
        private static readonly ConcurrentDictionary<string, StatusUpload> _uploads = new();
        private readonly ILogger<UploadService> _logger;
        private readonly string _uploadPath;
        private static readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);

        public UploadService(ILogger<UploadService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            Directory.CreateDirectory(_uploadPath);
        }

        public Task<Guid> IniciarNovoBatch()
        {
            var batch = new UploadBatch();
            _batches.TryAdd(batch.Id, batch);
            return Task.FromResult(batch.Id);
        }

        public async Task<StatusUpload> ProcessarUpload(IFormFile arquivo, Guid batchId)
        {
            if (!_batches.TryGetValue(batchId, out var batch))
                throw new ArgumentException("Batch não encontrado");

            var fileName = Path.GetFileName(arquivo.FileName);
            var filePath = Path.Combine(_uploadPath, fileName);
            
            var upload = new StatusUpload
            {
                NomeArquivo = fileName,
                Status = "PENDENTE",
                BatchId = batchId,
                CaminhoArquivo = filePath,
                TamanhoArquivo = arquivo.Length,
                DataInicio = DateTime.Now
            };

            _uploads.TryAdd(fileName, upload);
            batch.Arquivos.Add(upload);
            batch.TotalArquivos++;

            // Inicia o processamento assíncrono
            _ = Task.Run(async () =>
            {
                try
                {
                    upload.Status = "UPLOADING";
                    
                    // Lê o arquivo para um array de bytes de forma segura
                    byte[] fileBytes;
                    using (var ms = new MemoryStream())
                    {
                        // Copia o arquivo para o MemoryStream
                        await arquivo.OpenReadStream().CopyToAsync(ms);
                        fileBytes = ms.ToArray();
                    }

                    // Salva o arquivo usando o semáforo
                    await _fileLock.WaitAsync();
                    try
                    {
                        await File.WriteAllBytesAsync(filePath, fileBytes);
                    }
                    finally
                    {
                        _fileLock.Release();
                    }

                    upload.Status = "PROCESSANDO";
                    
                    // Simula processamento
                    await Task.Delay(1000);
                    
                    // Calcula o hash do arquivo
                    upload.HashArquivo = await CalcularHashArquivo(filePath);
                    
                    upload.Status = "CONCLUIDO";
                    upload.DataFim = DateTime.Now;
                    
                    batch.ArquivosProcessados++;
                    if (batch.ArquivosProcessados + batch.ArquivosComErro == batch.TotalArquivos)
                    {
                        batch.Status = "FINALIZADO";
                        batch.DataFinalizacao = DateTime.Now;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Erro ao processar arquivo {fileName}");
                    upload.Status = "ERRO";
                    upload.Mensagem = $"Erro ao processar arquivo: {ex.Message}\n\nDetalhes técnicos:\n{ex}";
                    upload.DataFim = DateTime.Now;
                    batch.ArquivosComErro++;

                    // Limpa o arquivo em caso de erro
                    try
                    {
                        await _fileLock.WaitAsync();
                        try
                        {
                            if (File.Exists(filePath))
                            {
                                File.Delete(filePath);
                            }
                        }
                        finally
                        {
                            _fileLock.Release();
                        }
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogError(deleteEx, $"Erro ao deletar arquivo {fileName} após falha no processamento");
                    }
                }
            });

            return upload;
        }

        public Task<UploadBatch?> ObterStatusBatch(Guid batchId)
        {
            return Task.FromResult(_batches.TryGetValue(batchId, out var batch) ? batch : null);
        }

        public Task<List<UploadBatch>> ObterTodosBatches()
        {
            return Task.FromResult(_batches.Values.OrderByDescending(x => x.DataCriacao).ToList());
        }

        public Task<bool> RetomarUpload(string nomeArquivo, Guid batchId)
        {
            if (!_uploads.TryGetValue(nomeArquivo, out var upload) || upload.BatchId != batchId)
                return Task.FromResult(false);

            upload.Status = "PENDENTE";
            upload.Tentativas++;
            upload.DataInicio = DateTime.Now;
            upload.DataFim = null;

            return Task.FromResult(true);
        }

        public Task<bool> CancelarUpload(string nomeArquivo, Guid batchId)
        {
            if (!_uploads.TryGetValue(nomeArquivo, out var upload) || upload.BatchId != batchId)
                return Task.FromResult(false);

            upload.Status = "CANCELADO";
            upload.DataFim = DateTime.Now;
            return Task.FromResult(true);
        }

        private async Task<string> CalcularHashArquivo(string filePath)
        {
            await _fileLock.WaitAsync();
            try
            {
                using var md5 = MD5.Create();
                using var stream = File.OpenRead(filePath);
                var hash = await md5.ComputeHashAsync(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            finally
            {
                _fileLock.Release();
            }
        }
    }
} 