namespace BattleTanks_Backend.Models.GameState;

public enum InputType
{
    MoveStart,   // Start moving in a direction
    MoveStop,    // Stop moving
    Shoot        // Fire a bullet
}

public class PlayerInput
{
    public required string ConnectionId { get; init; }
    public required InputType Type { get; init; }
    public Direction? Direction { get; init; }  // Required for MoveStart
    public long Timestamp { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public int SequenceNumber { get; init; }    // For input ordering/reconciliation
}
