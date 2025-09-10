using Microsoft.AspNetCore.SignalR;

namespace MultiSportTracker.Services
{
    public class LiveScoreHub : Hub
    {
        // example method to broadcast a score update
        public async Task BroadcastScoreUpdate(string message)
        {
            await Clients.All.SendAsync("ReceiveScoreUpdate", message);
        }
    }
}
