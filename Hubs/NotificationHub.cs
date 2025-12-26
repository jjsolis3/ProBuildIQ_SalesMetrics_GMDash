using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace SalesMetrics.Hubs
{
    public class NotificationHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            // Get user ID from claims
            var userId = Context.User?.FindFirst("Users_Id")?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                // Add user to their personal group (for targeted notifications)
                await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");

                // Get user's role and location for broadcast groups
                var roleId = Context.User?.FindFirst("RoleId")?.Value;
                var locationId = HttpContext.Session.GetInt32("LocationId")?.ToString();

                if (!string.IsNullOrEmpty(roleId))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"Role_{roleId}");
                }

                if (!string.IsNullOrEmpty(locationId))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"Location_{locationId}");
                }

                // Add to company-wide group
                await Groups.AddToGroupAsync(Context.ConnectionId, "AllUsers");
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Groups are automatically removed on disconnect
            await base.OnDisconnectedAsync(exception);
        }

        // Client can call this to mark notification as read
        public async Task MarkNotificationAsRead(int notificationId)
        {
            var userId = Context.User?.FindFirst("Users_Id")?.Value;
            // Notify other clients of this user that the notification was read
            if (!string.IsNullOrEmpty(userId))
            {
                await Clients.Group($"User_{userId}").SendAsync("NotificationRead", notificationId);
            }
        }
    }
}
