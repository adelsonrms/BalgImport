using System;
using System.Collections.Generic;

namespace BalgImport.Models
{
    public class UploadBatch
    {
        public Guid Id { get; set; }
        public DateTime DataCriacao { get; set; }
        public DateTime? DataFinalizacao { get; set; }
        public string Status { get; set; }
        public int TotalArquivos { get; set; }
        public int ArquivosProcessados { get; set; }
        public int ArquivosComErro { get; set; }
        public List<StatusUpload> Arquivos { get; set; }
        public string? MensagemErro { get; set; }

        public UploadBatch()
        {
            Id = Guid.NewGuid();
            DataCriacao = DateTime.Now;
            Status = "PENDENTE";
            Arquivos = new List<StatusUpload>();
        }
    }
} 