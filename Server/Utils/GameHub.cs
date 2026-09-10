using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Server.Utils;

public class GameHub : Hub
{
    private static readonly ConcurrentDictionary<string, string> UserConnections = new();

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            UserConnections[userId] = Context.ConnectionId;
        }

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            UserConnections.TryRemove(userId, out _);
        }

        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    public static Task AddToGroupByUserId(IHubContext<GameHub> hubContext, string userId, string groupName)
    {
        ArgumentNullException.ThrowIfNull(hubContext);
        return AddToGroupByUserIdInner(hubContext, userId, groupName);
    }

    public static Task RemoveFromGroupByUserId(IHubContext<GameHub> hubContext, string userId, string groupName)
    {
        ArgumentNullException.ThrowIfNull(hubContext);
        return RemoveFromGroupByUserIdInner(hubContext, userId, groupName);
    }

    private static async Task AddToGroupByUserIdInner(IHubContext<GameHub> hubContext, string userId, string groupName)
    {
        if (UserConnections.TryGetValue(userId, out var connectionId))
        {
            await hubContext.Groups.AddToGroupAsync(connectionId, groupName).ConfigureAwait(false);
        }
    }

    private static async Task RemoveFromGroupByUserIdInner(IHubContext<GameHub> hubContext, string userId, string groupName)
    {
        if (UserConnections.TryGetValue(userId, out var connectionId))
        {
            await hubContext.Groups.RemoveFromGroupAsync(connectionId, groupName).ConfigureAwait(false);
        }
    }
}
