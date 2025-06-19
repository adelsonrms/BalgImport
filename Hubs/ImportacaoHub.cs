using Microsoft.AspNetCore.SignalR;
using BalgImport.Models;

namespace BalgImport.Hubs
{
    public class ImportacaoHub : Hub
    {
        private readonly ILogger<ImportacaoHub> _logger;

        public ImportacaoHub(ILogger<ImportacaoHub> logger)
        {
            _logger = logger;
        }

        public async Task AtualizarStatus(object status)
        {
            _logger.LogInformation($"SignalR: Recebido status: {status}");
            await Clients.All.SendAsync("AtualizarStatus", status);
        }

        public async Task AtualizarResumo(object resumo)
        {
            _logger.LogInformation($"SignalR: AtualizarResumo: {resumo}");
            await Clients.All.SendAsync("AtualizarResumo", resumo);
        }

        public async Task AtualizarLoteAtual(object loteAtual)
        {
            _logger.LogInformation($"SignalR: AtualizarLoteAtual: {loteAtual}");
            await Clients.All.SendAsync("AtualizarLoteAtual", loteAtual);
        }

        public async Task AdicionarHistoricoLote(object loteFinalizado)
        {
            _logger.LogInformation($"SignalR: AdicionarHistoricoLote: {loteFinalizado}");
            await Clients.All.SendAsync("AdicionarHistoricoLote", loteFinalizado);
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation($"SignalR: Cliente conectado: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation($"SignalR: Cliente desconectado: {Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }
    }
} 