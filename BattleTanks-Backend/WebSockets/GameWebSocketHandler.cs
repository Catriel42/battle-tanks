using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using BattleTanks_Backend.Models;

namespace BattleTanks_Backend.WebSockets;

public class GameWebSocketHandler
{
    private readonly ConnectionManager _connectionManager;

    public GameWebSocketHandler(ConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public async Task HandleAsync(WebSocket socket)
    {
        var socketId = _connectionManager.AddSocket(socket);
        Console.WriteLine($"Socket Connected: {socketId}");

        var buffer = new byte[1024 * 4];

        try
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            while (!result.CloseStatus.HasValue)
            {
                var messageString = Encoding.UTF8.GetString(buffer, 0, result.Count);
                await ProcessMessageAsync(socketId, messageString);

                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }

            await socket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }
        catch (WebSocketException)
        {

        }
        finally
        {
            _connectionManager.RemoveSocket(socketId);
            Console.WriteLine($"Socket Disconnected: {socketId}");
            await BroadcastLeaveMessageAsync(socketId);
        }
    }

    private async Task ProcessMessageAsync(string senderId, string messageString)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var gameMessage = JsonSerializer.Deserialize<GameMessage>(messageString, options);

            if (gameMessage == null) return;

            string outboundMessageString = messageString;
            if (gameMessage.Type == "join")
            {
                var playerInfo = JsonSerializer.Deserialize<PlayerInfo>(gameMessage.Payload.GetRawText(), options);
                if (playerInfo != null)
                {
                    _connectionManager.SetUsername(senderId, playerInfo.Username);
                    await SendExistingPlayersToNewUserAsync(senderId, options);

                    var newPlayerInfo = playerInfo with { Id = senderId };
                    var newGameMessage = gameMessage with { Payload = JsonSerializer.SerializeToElement(newPlayerInfo, options) };
                    outboundMessageString = JsonSerializer.Serialize(newGameMessage, options);
                }
            }

            if (gameMessage.Type == "move")
            {
                var position = JsonSerializer.Deserialize<PlayerPosition>(gameMessage.Payload.GetRawText(), options);
                if (position != null)
                {
                    var newPosition = position with { Id = senderId };
                    var newGameMessage = gameMessage with { Payload = JsonSerializer.SerializeToElement(newPosition, options) };
                    outboundMessageString = JsonSerializer.Serialize(newGameMessage, options);
                }
            }

            await BroadcastMessageAsync(outboundMessageString, senderId);
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Error parsing JSON: {ex.Message}");
        }
    }

    private async Task SendExistingPlayersToNewUserAsync(string newUserId, JsonSerializerOptions options)
    {
        var newUserConnection = _connectionManager.GetConnectionById(newUserId);
        if (newUserConnection == null || newUserConnection.Socket.State != WebSocketState.Open) return;

        foreach (var pair in _connectionManager.GetAllConnections())
        {
            if (pair.Key != newUserId && pair.Value.Username != null)
            {
                var existingPlayerInfo = new PlayerInfo(pair.Key, pair.Value.Username);
                var joinMessage = new GameMessage("join", JsonSerializer.SerializeToElement(existingPlayerInfo, options));
                var messageString = JsonSerializer.Serialize(joinMessage, options);

                var bytes = Encoding.UTF8.GetBytes(messageString);
                await newUserConnection.Socket.SendAsync(new ArraySegment<byte>(bytes, 0, bytes.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }

    private async Task BroadcastMessageAsync(string message, string excludeId)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        var arraySegment = new ArraySegment<byte>(bytes, 0, bytes.Length);

        foreach (var pair in _connectionManager.GetAllConnections())
        {
            if (pair.Key != excludeId && pair.Value.Socket.State == WebSocketState.Open)
            {
                await pair.Value.Socket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }

    private async Task BroadcastLeaveMessageAsync(string socketId)
    {
        var leaveMessage = new GameMessage("leave", JsonSerializer.SerializeToElement(new PlayerInfo(socketId, "Disconnected")));
        var messageString = JsonSerializer.Serialize(leaveMessage);
        await BroadcastMessageAsync(messageString, socketId);
    }
}
