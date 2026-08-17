using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace BattleTanks_Backend.Hubs;

public class GameHub : Hub
{
    private static readonly ConcurrentDictionary<string, string> ConnectedPlayers = new();

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("ReceiveWelcome", new { id = Context.ConnectionId });
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        ConnectedPlayers.TryRemove(Context.ConnectionId, out var username);
        await Clients.Others.SendAsync("ReceivePlayerLeave", new
        {
            id = Context.ConnectionId,
            username = username ?? "Disconnected"
        });
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendPlayerJoin(string username)
    {
        ConnectedPlayers[Context.ConnectionId] = username;

        foreach (var kvp in ConnectedPlayers)
        {
            if (kvp.Key != Context.ConnectionId)
            {
                await Clients.Caller.SendAsync("ReceivePlayerJoin", new
                {
                    id = kvp.Key,
                    username = kvp.Value
                });
            }
        }

        await Clients.Others.SendAsync("ReceivePlayerJoin", new
        {
            id = Context.ConnectionId,
            username
        });
    }

    public async Task SendPlayerMove(double x, double y, string direction)
    {
        await Clients.Others.SendAsync("ReceivePlayerMove", new
        {
            id = Context.ConnectionId,
            x, y, direction
        });
    }

    public async Task SendChatMessage(string username, string text)
    {
        await Clients.All.SendAsync("ReceiveChatMessage", new
        {
            username,
            text,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }

    public async Task SendShoot(double x, double y, string direction)
    {
        await Clients.Others.SendAsync("ReceiveShoot", new
        {
            id = Context.ConnectionId,
            x, y, direction
        });
    }

    public async Task SendDestroyBlock(int row, int col)
    {
        await Clients.Others.SendAsync("ReceiveDestroyBlock", new
        {
            row, col,
            id = Context.ConnectionId
        });
    }

    public async Task SendPlayerHit(string targetId)
    {
        await Clients.All.SendAsync("ReceivePlayerHit", new
        {
            attackerId = Context.ConnectionId,
            targetId
        });
    }

    public async Task Ping()
    {
        await Clients.Caller.SendAsync("Pong", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }
}
