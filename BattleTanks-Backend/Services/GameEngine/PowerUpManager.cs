using BattleTanks_Backend.Models.GameState;

namespace BattleTanks_Backend.Services.GameEngine;

public class PowerUpManager
{
    private readonly GameRoomState _room;
    private readonly Random _random = new();
    
    // Events that occurred during the current tick
    public List<PowerUpSpawnedEvent> SpawnedEvents { get; } = [];
    public List<PowerUpCollectedEvent> CollectedEvents { get; } = [];
    
    public PowerUpManager(GameRoomState room)
    {
        _room = room;
    }
    
    public void ClearEvents()
    {
        SpawnedEvents.Clear();
        CollectedEvents.Clear();
    }
    
    public void TrySpawnPowerUp()
    {
        if (_room.PowerUps.Count >= GameRoomState.MaxPowerUpsOnMap)
            return;
        
        if (DateTime.UtcNow < _room.NextPowerUpSpawn)
            return;
        
        var (x, y) = FindRandomSpawnPosition();
        if (x < 0 || y < 0)
            return;
        
        var powerUp = new PowerUpState
        {
            Id = Guid.NewGuid(),
            Type = PowerUpType.ExtraLife,
            X = x,
            Y = y,
            SpawnedAt = DateTime.UtcNow,
            IsCollected = false
        };
        
        _room.PowerUps.Add(powerUp);
        
        var nextSpawnSeconds = _random.Next(15, 31);
        _room.NextPowerUpSpawn = DateTime.UtcNow.AddSeconds(nextSpawnSeconds);
        
        SpawnedEvents.Add(new PowerUpSpawnedEvent(
            powerUp.Id,
            powerUp.Type.ToString(),
            powerUp.X,
            powerUp.Y,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        ));
    }
    
    public void CheckCollection(TankState tank)
    {
        if (!tank.IsAlive)
            return;
        
        var collectedPowerUps = new List<PowerUpState>();
        
        foreach (var powerUp in _room.PowerUps)
        {
            if (powerUp.IsCollected)
                continue;
            
            // Check collision: tank (40x40) and power-up (30x30)
            if (IsColliding(tank.X, tank.Y, 40, 40, powerUp.X, powerUp.Y, 30, 30))
            {
                collectedPowerUps.Add(powerUp);
                
                // Apply power-up effect
                switch (powerUp.Type)
                {
                    case PowerUpType.ExtraLife:
                        tank.Lives = Math.Min(tank.Lives + 1, _room.MaxHealth + 1);
                        break;
                }
                
                CollectedEvents.Add(new PowerUpCollectedEvent(
                    powerUp.Id,
                    tank.PlayerId.ToString(),
                    tank.Username,
                    tank.Lives,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                ));
            }
        }
        
        // Remove collected power-ups
        foreach (var powerUp in collectedPowerUps)
        {
            powerUp.IsCollected = true;
            _room.RemovePowerUp(powerUp.Id);
        }
    }
    
    private static bool IsColliding(double x1, double y1, int w1, int h1, double x2, double y2, int w2, int h2)
    {
        return x1 < x2 + w2 && x1 + w1 > x2 && y1 < y2 + h2 && y1 + h1 > y2;
    }
    
    private (double x, double y) FindRandomSpawnPosition()
    {
        const int maxAttempts = 20;
        const int tileSize = 40;
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var col = _random.Next(2, _room.MapWidth - 2);
            var row = _random.Next(2, _room.MapHeight - 2);
            
            // Check if tile is free (not blocked)
            if (!_room.IsTileBlocking(row, col))
            {
                var x = col * tileSize;
                var y = row * tileSize;
                
                // Check if it's not too close to spawn points
                if (!IsTooCloseToSpawnPoints(x, y))
                {
                    return (x, y);
                }
            }
        }
        
        return (-1, -1);
    }
    
    private bool IsTooCloseToSpawnPoints(double x, double y)
    {
        const int minDistance = 3 * 40;
        
        foreach (var (spawnX, spawnY) in _room.SpawnPoints)
        {
            var dist = Math.Sqrt(Math.Pow(x - spawnX, 2) + Math.Pow(y - spawnY, 2));
            if (dist < minDistance)
                return true;
        }
        
        return false;
    }
}

// DTOs for events
public record PowerUpSpawnedEvent(
    Guid Id,
    string Type,
    double X,
    double Y,
    long Timestamp
);

public record PowerUpCollectedEvent(
    Guid Id,
    string PlayerId,
    string Username,
    int NewLives,
    long Timestamp
);
