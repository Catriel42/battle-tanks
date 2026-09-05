namespace BattleTanks_Backend.Models.GameState;

public class BulletState
{
    public required string Id { get; init; }
    public required string OwnerId { get; init; } // ConnectionId of shooter
    public double X { get; set; }
    public double Y { get; set; }
    public Direction Direction { get; init; }
    public int Damage { get; init; } = 1;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    
    // For calculating movement
    public double Speed { get; init; } = 10.0;
    
    public void Update()
    {
        var (dx, dy) = Direction.ToVector();
        X += dx * Speed;
        Y += dy * Speed;
    }
    
    // Check if bullet is out of bounds
    public bool IsOutOfBounds(int worldWidth, int worldHeight)
    {
        return X < 0 || Y < 0 || X >= worldWidth || Y >= worldHeight;
    }
}
