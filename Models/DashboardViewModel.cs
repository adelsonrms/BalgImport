using System;
using System.Collections.Generic;

namespace BalgImport.Models
{
    public class DashboardViewModel
    {
        public List<LoteViewModel> Lotes { get; set; } = new List<LoteViewModel>();
        public IndicadoresViewModel Indicadores { get; set; } = new IndicadoresViewModel();
    }

    public class LoteViewModel
    {
        public Guid Id { get; set; }
        public DateTime DataCriacao { get; set; }
        public DateTime? DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
        public string UsuarioId { get; set; }
        public string UsuarioNome { get; set; }
        public string Status { get; set; }
        public int TotalArquivos { get; set; }
        public int ArquivosProcessados { get; set; }
        public int ArquivosComErro { get; set; }
        public string MensagemErro { get; set; }
    }

    public class IndicadoresViewModel
    {
        public int TotalLotes { get; set; }
        public int TotalArquivos { get; set; }
        public int ArquivosProcessados { get; set; }
        public int ArquivosComErro { get; set; }
        public int LotesEmAndamento { get; set; }
        public int LotesConcluidos { get; set; }
        public int LotesComErro { get; set; }
    }
} 