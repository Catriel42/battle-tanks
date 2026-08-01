using System.Net.WebSockets;
public class PlayerConnection
{
    public WebSocket Socket { get; }
    public string? Username { get; set; }

    public PlayerConnection(WebSocket socket)
    {
        Socket = socket;
    }
}
