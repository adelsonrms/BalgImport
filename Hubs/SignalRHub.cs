using Microsoft.AspNetCore.SignalR;

namespace BalgImport.Hubs
{
    public class SignalRHub : Hub
    {
        public async Task AtualizarStatus(object status)
        {
            await Clients.All.SendAsync("AtualizarStatus", status);
        }
    }
} 