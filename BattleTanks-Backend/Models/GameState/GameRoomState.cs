using System.Collections.Concurrent;

namespace BattleTanks_Backend.Models.GameState;

public enum GameStatus
{
    Waiting,
    Starting,   // Countdown before game starts
    Playing,
    Finished
}

public class GameRoomState
{
    public required string RoomId { get; init; }
    public required Guid SessionId { get; init; }
    public GameStatus Status { get; set; } = GameStatus.Waiting;
    
    // Map data
    public int[][] MapGrid { get; set; } = [];
    public int MapWidth { get; set; }
    public int MapHeight { get; set; }
    public HashSet<(int row, int col)> DestroyedBlocks { get; } = [];
    
    // Players and projectiles
    public ConcurrentDictionary<string, TankState> Tanks { get; } = new();
    public ConcurrentBag<BulletState> Bullets { get; private set; } = [];
    public ConcurrentBag<PowerUpState> PowerUps { get; private set; } = [];
    
    // Host tracking - the first player to join is the host
    public string? HostConnectionId { get; set; }
    
    // Timing
    public long CurrentTick { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    
    // Game configuration
    public int Lives { get; set; } = 3;
    public int MaxPlayers { get; set; } = 4;
    public int MinPlayers { get; set; } = 2;
    
    // Constants (can be moved to GameConstants)
    public int TickRate { get; init; } = 60;
    public double TankSpeed { get; init; } = 5.0;
    public double BulletSpeed { get; init; } = 10.0;
    public int ShootCooldownMs { get; init; } = 500;
    public int MaxHealth { get; init; } = 3;
    public int TileSize { get; init; } = 40;
    public int TankSize { get; init; } = 40;
    
    // Spawn points (4 corners)
    public (double x, double y)[] SpawnPoints { get; private set; } = [];
    
    // Elimination order tracking (for final positions)
    public List<string> EliminationOrder { get; } = [];
    
    // Power-up tracking
    public DateTime NextPowerUpSpawn { get; set; } = DateTime.UtcNow;
    public const int MaxPowerUpsOnMap = 2;
    public const int PowerUpSpawnIntervalSeconds = 15;
    
    public void InitializeSpawnPoints()
    {
        // Find valid spawn points in the 4 corners of the map
        // A valid spawn requires the tank (40x40) to not overlap any blocking tiles
        
        var spawnList = new List<(double x, double y)>();
        
        // Define search areas for each corner (row range, col range)
        var corners = new[]
        {
            (rowStart: 1, rowEnd: 5, colStart: 1, colEnd: 5),                           // Top-left
            (rowStart: 1, rowEnd: 5, colStart: MapWidth - 6, colEnd: MapWidth - 2),     // Top-right
            (rowStart: MapHeight - 6, rowEnd: MapHeight - 2, colStart: 1, colEnd: 5),   // Bottom-left
            (rowStart: MapHeight - 6, rowEnd: MapHeight - 2, colStart: MapWidth - 6, colEnd: MapWidth - 2)  // Bottom-right
        };
        
        foreach (var (rowStart, rowEnd, colStart, colEnd) in corners)
        {
            var found = false;
            
            // Search for a valid position in this corner
            for (var row = rowStart; row <= rowEnd && !found; row++)
            {
                for (var col = colStart; col <= colEnd && !found; col++)
                {
                    if (IsValidSpawnTile(row, col))
                    {
                        // Convert tile position to world position (top-left of tile)
                        var worldX = col * TileSize;
                        var worldY = row * TileSize;
                        spawnList.Add((worldX, worldY));
                        found = true;
                    }
                }
            }
            
            // Fallback if no valid position found in corner
            if (!found)
            {
                // Use a default position offset from the corner
                var fallbackX = (colStart + 1) * TileSize;
                var fallbackY = (rowStart + 1) * TileSize;
                spawnList.Add((fallbackX, fallbackY));
            }
        }
        
        SpawnPoints = spawnList.ToArray();
    }
    
    /// <summary>
    /// Check if a tank can spawn at the given tile position.
    /// The tank occupies 40x40 pixels, which is exactly 1 tile.
    /// </summary>
    private bool IsValidSpawnTile(int row, int col)
    {
        // Check bounds
        if (row < 0 || row >= MapHeight || col < 0 || col >= MapWidth)
            return false;
        
        // Check the tile itself
        if (MapGrid[row][col] != 0)
            return false;
        
        return true;
    }
    
    public (double x, double y) GetSpawnPoint(int playerIndex)
    {
        if (SpawnPoints.Length == 0)
        {
            InitializeSpawnPoints();
        }
        return SpawnPoints[playerIndex % SpawnPoints.Length];
    }
    
    public int GetTileAt(int row, int col)
    {
        if (row < 0 || row >= MapHeight || col < 0 || col >= MapWidth)
            return 1; // Out of bounds = wall
            
        if (DestroyedBlocks.Contains((row, col)))
            return 0; // Destroyed = empty
            
        return MapGrid[row][col];
    }
    
    public bool IsTileBlocking(int row, int col)
    {
        var tile = GetTileAt(row, col);
        return tile > 0; // Both steel (1) and brick (2) block movement
    }
    
    public bool IsTileDestructible(int row, int col)
    {
        var tile = GetTileAt(row, col);
        return tile == 2 && !DestroyedBlocks.Contains((row, col));
    }
    
    public void DestroyBlock(int row, int col)
    {
        if (IsTileDestructible(row, col))
        {
            DestroyedBlocks.Add((row, col));
        }
    }
    
    public int GetAlivePlayersCount()
    {
        return Tanks.Values.Count(t => t.IsAlive && !t.IsEliminated);
    }
    
    public TankState? GetWinner()
    {
        var aliveTanks = Tanks.Values.Where(t => !t.IsEliminated).ToList();
        return aliveTanks.Count == 1 ? aliveTanks[0] : null;
    }
    
    public void ClearBullets()
    {
        Bullets = [];
    }
    
    public void RemoveBullet(BulletState bullet)
    {
        var newBullets = new ConcurrentBag<BulletState>(Bullets.Where(b => b.Id != bullet.Id));
        Bullets = newBullets;
    }
    
    public void ClearPowerUps()
    {
        PowerUps = [];
    }
    
    public void RemovePowerUp(Guid powerUpId)
    {
        var newPowerUps = new ConcurrentBag<PowerUpState>(PowerUps.Where(p => p.Id != powerUpId));
        PowerUps = newPowerUps;
    }
}
