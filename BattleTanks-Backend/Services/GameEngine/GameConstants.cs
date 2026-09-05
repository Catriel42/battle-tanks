namespace BattleTanks_Backend.Services.GameEngine;

public static class GameConstants
{
    // Tick rate
    public const int TickRate = 60;
    public const double TickDurationMs = 1000.0 / TickRate; // ~16.67ms
    
    // Map
    public const int TileSize = 40;
    public const int DefaultMapWidth = 30;
    public const int DefaultMapHeight = 20;
    
    // Tank
    public const int TankSize = 40;
    public const double TankSpeed = 4.0;  // Pixels per tick
    public const int TankMaxHealth = 3;
    public const int DefaultLives = 3;
    public const int ShootCooldownMs = 500;
    
    // Bullet
    public const double BulletSpeed = 8.0;  // Pixels per tick
    public const int BulletDamage = 1;
    public const int BulletSize = 8;
    public const int MaxBulletLifetimeMs = 5000;  // 5 seconds max
    
    // Game rules
    public const int MinPlayers = 2;
    public const int MaxPlayers = 4;
    public const int CountdownSeconds = 3;
    public const int RespawnInvulnerabilityMs = 2000;  // 2 seconds invulnerability after respawn
    
    // Tile types
    public const int TileEmpty = 0;
    public const int TileSteel = 1;  // Indestructible wall
    public const int TileBrick = 2;  // Destructible wall
    
    // Network
    public const int MaxInputQueueSize = 64;
    public const int StateSnapshotIntervalTicks = 3;  // Send full state every 3 ticks (~50ms)
}
