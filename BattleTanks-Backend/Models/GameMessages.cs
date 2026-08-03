using System.Text.Json;
using System.Text.Json.Serialization;

namespace BattleTanks_Backend.Models;

public record GameMessage(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("payload")] JsonElement Payload
);

public record PlayerPosition(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y
);

public record ChatMessage(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("timestamp")] long Timestamp
);

public record PlayerInfo(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("username")] string Username
);

public record GameState(
    [property: JsonPropertyName("status")] string Status
);
