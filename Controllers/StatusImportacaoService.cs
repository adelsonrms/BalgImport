using BalgImport.Hubs;

using Microsoft.AspNetCore.SignalR;

using Projeto_BALG_Import.Services;

namespace Projeto_BALG_Import.Controllers
{
    public static class StatusEtapas
    {
        public const string Inicio = "INICIO";
        public const string Processando = "PROCESSANDO";
        public const string Finalizado = "FINALIZADO";
    }

    public static class StatusTipos
    {
        public const string Iniciado = "INICIADO";
        public const string Processando = "PROCESSANDO";
        public const string Concluido = "CONCLUIDO";
        public const string Erro = "ERRO";
        public const string Cancelado = "CANCELADO";
        public const string Pendente = "PENDENTE";
    }

    public class StatusImportacaoService
    {
        private readonly IHubContext<ImportacaoHub> _hubContext;

        public StatusImportacaoService(IHubContext<ImportacaoHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task EnviarStatus(LoteProcessamento lote,
            string etapa,
            string status,
            string mensagem)
        {
            await _hubContext.Clients.All.SendAsync("ReceberStatusImportacao", 
                new StatusImportacao
                {
                    IdUpload = lote.idUpload,
                    IdLote = lote.IdLote,
                    IdArquivo = lote.idArq,
                    NomeArquivo = lote.codProc,
                    Etapa = etapa,
                    Status = status,
                    Mensagem = mensagem,
                    DataHora = DateTime.Now,
                    DataInicio = lote.DataInicio,
                    DataFim = lote.DataFim,
                    TotalArquivos = lote.Arquivos?.Count ?? 0,
                    ArquivosProcessados = lote.ArquivosProcessados, 
                    Usuario = lote.Usuario

                });
        }

        public async Task EnviarStatusInicial(LoteProcessamento lote)
        {
            await EnviarStatus(
                lote,
                etapa: "INICIO",
                status: "INICIADO",
                mensagem: "Iniciando a importação dos arquivos"
            );
        }

        public async Task EnviarStatusProcessamento(LoteProcessamento lote)
        {
            await EnviarStatus(
                lote,
                etapa: "PROCESSANDO",
                status: "PROCESSANDO",
                mensagem: $"Processando arquivos ({lote.ArquivosProcessados}/{lote.Arquivos?.Count ?? 0})"
            );
        }

        public async Task EnviarStatusConcluido(LoteProcessamento lote)
        {
            await EnviarStatus(
                lote,
                etapa: "FINALIZADO",
                status: "CONCLUIDO",
                mensagem: "Importação concluída com sucesso"
            );
        }

        public async Task EnviarStatusErro(LoteProcessamento lote, string mensagemErro)
        {
            await EnviarStatus(
                lote,
                etapa: "FINALIZADO",
                status: "ERRO",
                mensagem: mensagemErro
            );
        }

        public async Task EnviarResumoUpload(object resumo)
        {
            await _hubContext.Clients.All.SendAsync("AtualizarResumo", resumo);
        }

        public async Task EnviarLoteAtual(object loteAtual)
        {
            await _hubContext.Clients.All.SendAsync("AtualizarLoteAtual", loteAtual);
        }

        public async Task EnviarHistoricoLote(object loteFinalizado)
        {
            await _hubContext.Clients.All.SendAsync("AdicionarHistoricoLote", loteFinalizado);
        }
    }
}