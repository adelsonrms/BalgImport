using System.Text.Json;
using BalgImport.Models;

namespace BalgImport.Services
{
    public interface IStorageService
    {
        Task<List<UploadBatch>> LoadBatches();
        Task SaveBatches(List<UploadBatch> batches);
        Task<UploadBatch?> GetBatchById(Guid id);
        Task SaveBatch(UploadBatch batch);
    }

    public class StorageService : IStorageService
    {
        private readonly string _storagePath;
        private readonly ILogger<StorageService> _logger;
        private static readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);

        public StorageService(ILogger<StorageService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _storagePath = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            Directory.CreateDirectory(_storagePath);
        }

        public async Task<List<UploadBatch>> LoadBatches()
        {
            var filePath = Path.Combine(_storagePath, "batches.json");
            if (!File.Exists(filePath))
                return new List<UploadBatch>();

            await _fileLock.WaitAsync();
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<List<UploadBatch>>(json) ?? new List<UploadBatch>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar lotes do arquivo");
                return new List<UploadBatch>();
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task SaveBatches(List<UploadBatch> batches)
        {
            var filePath = Path.Combine(_storagePath, "batches.json");
            await _fileLock.WaitAsync();
            try
            {
                var json = JsonSerializer.Serialize(batches, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar lotes no arquivo");
                throw;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<UploadBatch?> GetBatchById(Guid id)
        {
            var batches = await LoadBatches();
            return batches.FirstOrDefault(b => b.Id == id);
        }

        public async Task SaveBatch(UploadBatch batch)
        {
            var batches = await LoadBatches();
            var existingBatch = batches.FirstOrDefault(b => b.Id == batch.Id);
            
            if (existingBatch != null)
            {
                var index = batches.IndexOf(existingBatch);
                batches[index] = batch;
            }
            else
            {
                batches.Add(batch);
            }

            await SaveBatches(batches);
        }
    }
} 