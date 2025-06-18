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