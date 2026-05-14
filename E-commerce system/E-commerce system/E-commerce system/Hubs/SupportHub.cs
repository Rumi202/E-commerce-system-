using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace E_commerce_system.Hubs
{
    public class SupportHub : Hub
    {
        public async Task SendMessage(int ticketId, string user, string message)
        {
            // Broadcast message to everyone in the ticket's group
            await Clients.Group(ticketId.ToString()).SendAsync("ReceiveMessage", user, message);
        }

        public async Task JoinTicketGroup(int ticketId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, ticketId.ToString());
        }

        public async Task LeaveTicketGroup(int ticketId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, ticketId.ToString());
        }
    }
}
