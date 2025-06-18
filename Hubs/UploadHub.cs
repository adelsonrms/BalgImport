using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace BalgImport.Hubs
{
    public class UploadHub : Hub
    {
        public override Task OnConnectedAsync()
        {
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            return base.OnDisconnectedAsync(exception);
        }

        public async Task StatusChanged(string batchId, string status, int arquivosProcessados, int totalArquivos)
        {
            await Clients.All.SendAsync("statusChanged", new
            {
                batchId,
                status,
                arquivosProcessados,
                totalArquivos
            });
        }
    }
} 