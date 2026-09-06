namespace BattleTanks_Backend.Models.GameState;

public class TankState
{
    public required string ConnectionId { get; set; }
    public required Guid PlayerId { get; set; }
    public required string Username { get; set; }
    
    // Position
    public double X { get; set; }
    public double Y { get; set; }
    public Direction Direction { get; set; } = Direction.Up;
    
    // Movement state
    public bool IsMoving { get; set; }
    public Direction? MoveDirection { get; set; }
    
    // Health and lives
    public int Health { get; set; } = 3;
    public int Lives { get; set; } = 3;
    public bool IsAlive => Health > 0 && Lives > 0;
    public bool IsEliminated => Lives <= 0;
    
    // Spawn point
    public double SpawnX { get; set; }
    public double SpawnY { get; set; }
    
    // Combat
    public DateTime LastShotTime { get; set; } = DateTime.MinValue;
    public bool CanShoot(int cooldownMs) => (DateTime.UtcNow - LastShotTime).TotalMilliseconds >= cooldownMs;
    
    // Stats for this game
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int ShotsFired { get; set; }
    public int ShotsHit { get; set; }
    public int BlocksDestroyed { get; set; }
    public int DamageDealt { get; set; }
    public int DamageTaken { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EliminatedAt { get; set; }
    
    // Calculated property for survival time
    public long SurvivalTimeMs => (long)((EliminatedAt ?? DateTime.UtcNow) - JoinedAt).TotalMilliseconds;
    
    public void Respawn()
    {
        X = SpawnX;
        Y = SpawnY;
        Direction = Direction.Up;
        Health = 3; // Reset health, but keep Lives counter
        IsMoving = false;
        MoveDirection = null;
    }
    
    public void TakeDamage(int damage)
    {
        Health -= damage;
        DamageTaken += damage;
        
        if (Health <= 0)
        {
            Health = 0;
            Deaths++;
            Lives--;
            
            if (Lives <= 0)
            {
                EliminatedAt = DateTime.UtcNow;
            }
        }
    }
}
