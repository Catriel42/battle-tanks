using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace BattleTanks_Backend.WebSockets;

public class PlayerConnection
{
    public WebSocket Socket { get; }
    public string? Username { get; set; }

    public PlayerConnection(WebSocket socket)
    {
        Socket = socket;
    }
}

public class ConnectionManager
{
    private readonly ConcurrentDictionary<string, PlayerConnection> _connections = new();

    public string AddSocket(WebSocket socket)
    {
        var id = Guid.NewGuid().ToString();
        _connections.TryAdd(id, new PlayerConnection(socket));
        return id;
    }

    public void RemoveSocket(string id)
    {
        _connections.TryRemove(id, out _);
    }

    public ConcurrentDictionary<string, PlayerConnection> GetAllConnections()
    {
        return _connections;
    }

    public PlayerConnection? GetConnectionById(string id)
    {
        _connections.TryGetValue(id, out var connection);
        return connection;
    }

    public void SetUsername(string id, string username)
    {
        if (_connections.TryGetValue(id, out var connection))
        {
            connection.Username = username;
        }
    }
}
