namespace BalgImport.Models
{
    public class StatusUpdate
    {
        public long idUpload { get; set; }
        public int IdLote { get; set; }
        public int IdArquivo { get; set; }
        public string NomeArquivo { get; set; }
        public string Etapa { get; set; }
        public string Status { get; set; }
        public string Mensagem { get; set; }
        public DateTime DataHora { get; set; }
        public int TotalArquivos { get; set; }
        public int ArquivosProcessados { get; set; }
    }
} 