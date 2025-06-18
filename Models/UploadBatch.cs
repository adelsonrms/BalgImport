using System;
using System.Collections.Generic;
using System.Linq;

namespace BalgImport.Models
{
    public class UploadBatch
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string UsuarioId { get; set; }
        public string UsuarioNome { get; set; }
        public DateTime DataCriacao { get; set; } = DateTime.Now;
        public string Status { get; set; } = "PENDENTE";
        public DateTime DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
        public List<UploadStatus> Arquivos { get; set; } = new List<UploadStatus>();
        public string? MensagemErro { get; set; }
        public int TotalArquivos => Arquivos.Count;
        public int ArquivosProcessados => Arquivos.Count(a => a.Status == "CONCLUIDO");
        public int ArquivosComErro => Arquivos.Count(a => a.Status == "ERRO");

        public UploadBatch()
        {
            Status = "PENDENTE";
        }
    }
} 