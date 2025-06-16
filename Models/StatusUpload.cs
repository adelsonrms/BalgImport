using System;

namespace BalgImport.Models
{
    public class StatusUpload
    {
        public string NomeArquivo { get; set; }
        public string Status { get; set; }
        public string Mensagem { get; set; }
        public int Progresso { get; set; }
        public DateTime DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
        public Guid BatchId { get; set; }
        public int Tentativas { get; set; }
        public string CaminhoArquivo { get; set; }
        public long TamanhoArquivo { get; set; }
        public string HashArquivo { get; set; }
    }
} 