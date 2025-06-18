using System;

namespace BalgImport.Models
{
    public class UploadStatus
    {
        public string NomeArquivo { get; set; }
        public long Tamanho { get; set; }
        public string Status { get; set; }
        public string Hash { get; set; }
        public DateTime DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
        public string MensagemErro { get; set; }
    }
} 