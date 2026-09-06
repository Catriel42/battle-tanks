namespace BattleTanks_Backend.Models.GameState;

public enum PowerUpType
{
    ExtraLife
}

public class PowerUpState
{
    public required Guid Id { get; init; }
    public required PowerUpType Type { get; init; }
    public required double X { get; set; }
    public required double Y { get; set; }
    public required DateTime SpawnedAt { get; init; }
    public bool IsCollected { get; set; }
}
