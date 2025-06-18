using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using BalgImport.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;

namespace BalgImport.Services
{
    public interface IUploadService
    {
        Task<UploadBatch> IniciarNovoBatch(string usuarioId, string usuarioNome);
        Task<UploadStatus> ProcessarUpload(IFormFile file, Guid batchId);
        Task<UploadBatch> ObterStatusBatch(Guid batchId);
        Task<IEnumerable<UploadBatch>> ObterTodosBatches();
        Task RetomarUpload(Guid batchId);
        Task CancelarUpload(Guid batchId);
    }

    public class UploadService : IUploadService
    {
        private readonly ILogger<UploadService> _logger;
        private readonly IStorageService _storageService;
        private readonly List<UploadBatch> _batches = new List<UploadBatch>();
        private readonly IHubContext<Hubs.UploadHub> _hubContext;

        public UploadService(ILogger<UploadService> logger, IStorageService storageService, IHubContext<Hubs.UploadHub> hubContext)
        {
            _logger = logger;
            _storageService = storageService;
            _hubContext = hubContext;
        }

        private async Task NotifyStatusChange(UploadBatch batch)
        {
            await _hubContext.Clients.All.SendAsync("statusChanged", new
            {
                batchId = batch.Id,
                status = batch.Status,
                arquivosProcessados = batch.ArquivosProcessados,
                arquivosComErro = batch.ArquivosComErro,
                totalArquivos = batch.TotalArquivos,
                arquivos = batch.Arquivos.Select(a => new
                {
                    nomeArquivo = a.NomeArquivo,
                    status = a.Status,
                    mensagemErro = a.MensagemErro
                }).ToList()
            });
        }

        public async Task<UploadBatch> IniciarNovoBatch(string usuarioId, string usuarioNome)
        {
            var batch = new UploadBatch
            {
                UsuarioId = usuarioId,
                UsuarioNome = usuarioNome,
                Status = "PROCESSANDO",
                DataInicio = DateTime.Now
            };

            await _storageService.SaveBatch(batch);
            _batches.Add(batch);
            await NotifyStatusChange(batch);
            return batch;
        }

        public async Task<UploadStatus> ProcessarUpload(IFormFile file, Guid batchId)
        {
            var batch = _batches.FirstOrDefault(b => b.Id == batchId);
            if (batch == null)
                throw new Exception("Batch não encontrado");

            var uploadStatus = new UploadStatus
            {
                NomeArquivo = file.FileName,
                Tamanho = file.Length,
                Status = "PROCESSANDO",
                DataInicio = DateTime.Now
            };

            batch.Arquivos.Add(uploadStatus);
            batch.Status = "PROCESSANDO";
            await NotifyStatusChange(batch);

            try
            {
                // Processa o arquivo
                using var stream = file.OpenReadStream();
                using var sha256 = SHA256.Create();
                uploadStatus.Hash = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLower();
                uploadStatus.Status = "CONCLUIDO";
                uploadStatus.DataFim = DateTime.Now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar arquivo {FileName}", file.FileName);
                uploadStatus.Status = "ERRO";
                uploadStatus.MensagemErro = ex.Message;
                uploadStatus.DataFim = DateTime.Now;
            }

            await _storageService.SaveBatch(batch);
            await NotifyStatusChange(batch);
            return uploadStatus;
        }

        public async Task<UploadBatch> ObterStatusBatch(Guid batchId)
        {
            var batch = _batches.FirstOrDefault(b => b.Id == batchId);
            if (batch == null)
            {
                throw new Exception($"Lote {batchId} não encontrado");
            }
            return batch;
        }

        public async Task<IEnumerable<UploadBatch>> ObterTodosBatches()
        {
            return _batches.ToList();
        }

        public async Task RetomarUpload(Guid batchId)
        {
            var batch = _batches.FirstOrDefault(b => b.Id == batchId);
            if (batch != null)
            {
                foreach (var arquivo in batch.Arquivos.Where(a => a.Status == "ERRO"))
                {
                    arquivo.Status = "PENDENTE";
                    arquivo.MensagemErro = null;
                    arquivo.DataInicio = DateTime.Now;
                    arquivo.DataFim = null;
                }
                batch.Status = "PROCESSANDO";
                await _storageService.SaveBatch(batch);
                await NotifyStatusChange(batch);
            }
        }

        public async Task CancelarUpload(Guid batchId)
        {
            var batch = _batches.FirstOrDefault(b => b.Id == batchId);
            if (batch != null)
            {
                foreach (var arquivo in batch.Arquivos.Where(a => a.Status == "PENDENTE"))
                {
                    arquivo.Status = "CANCELADO";
                    arquivo.DataFim = DateTime.Now;
                }
                batch.Status = "CANCELADO";
                await _storageService.SaveBatch(batch);
                await NotifyStatusChange(batch);
            }
        }
    }
} 