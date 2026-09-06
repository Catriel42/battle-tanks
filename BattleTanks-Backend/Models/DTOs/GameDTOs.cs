using BattleTanks_Backend.Models.GameState;

namespace BattleTanks_Backend.Models.DTOs;

// ==================== Input DTOs (Client → Server) ====================

public record PlayerInputDto(
    string Type,        // "move_start", "move_stop", "shoot"
    string? Direction,  // "up", "down", "left", "right" (for move_start)
    int SequenceNumber
);

// ==================== State Snapshot DTOs (Server → Client) ====================

public record TankDto(
    string Id,          // ConnectionId
    Guid PlayerId,
    string Username,
    double X,
    double Y,
    string Direction,   // "up", "down", "left", "right"
    bool IsMoving,
    int Health,
    int Lives,
    bool IsAlive,
    bool IsEliminated
);

public record BulletDto(
    string Id,
    string OwnerId,     // ConnectionId of shooter
    double X,
    double Y,
    string Direction
);

public record GameStateSnapshot(
    long Tick,
    string Status,      // "waiting", "starting", "playing", "finished"
    long ServerTime,
    TankDto[] Tanks,
    BulletDto[] Bullets,
    int[] DestroyedBlocks  // Flattened: [row1, col1, row2, col2, ...]
);

// ==================== Event DTOs (Server → Client) ====================

public record GameStartingEvent(
    int CountdownSeconds,
    TankDto[] Tanks,
    int MapWidth,
    int MapHeight,
    int[][] MapGrid
);

public record GameStartedEvent(
    long StartTime,
    TankDto[] Tanks
);

public record PlayerJoinedEvent(
    TankDto Tank,
    int TotalPlayers
);

public record PlayerLeftEvent(
    string PlayerId,
    string Username,
    int TotalPlayers
);

public record HostChangedEvent(
    string NewHostId
);

public record PlayerHitEvent(
    string VictimId,
    string AttackerId,
    int Damage,
    int RemainingHealth,
    int RemainingLives
);

public record PlayerKilledEvent(
    string VictimId,
    string VictimUsername,
    string KillerId,
    string KillerUsername,
    int VictimLivesRemaining
);

public record PlayerEliminatedEvent(
    string PlayerId,
    string Username,
    int Position,       // Final position (4th, 3rd, 2nd)
    int PlayersRemaining
);

public record PlayerRespawnedEvent(
    string PlayerId,
    double X,
    double Y
);

public record BlockDestroyedEvent(
    int Row,
    int Col,
    string? DestroyerId // ConnectionId of destroyer, null if destroyed by environment
);

public record BulletFiredEvent(
    BulletDto Bullet
);

public record BulletHitEvent(
    string BulletId,
    string? HitPlayerId,    // Null if hit wall/block
    int? HitRow,            // Set if hit block
    int? HitCol
);

public record GameOverEvent(
    string? WinnerId,
    string? WinnerUsername,
    PlayerStatsDto[] FinalStats,
    long DurationMs
);

public record PlayerStatsDto(
    Guid PlayerId,
    string Username,
    int Position,       // 1st, 2nd, 3rd, 4th
    int Kills,
    int Deaths,
    int ShotsFired,
    int ShotsHit,
    double Accuracy,
    int BlocksDestroyed,
    int DamageDealt,
    int DamageTaken,
    long SurvivalTimeMs
);

// ==================== Lobby/Room Events ====================

public record RoomStateEvent(
    Guid RoomId,
    string Status,
    PlayerInfoDto[] Players,
    int MaxPlayers,
    int MinPlayers,
    bool CanStart,
    string? HostId
);

public record PlayerInfoDto(
    Guid PlayerId,
    string Username,
    bool IsHost,
    bool IsReady
);

// ==================== Chat Events ====================

public record ChatMessageDto(
    string Text
);

public record ChatMessageEvent(
     string Username,
    string Text,
    long Timestamp
);

// ==================== MQTT Events ====================

public record PowerUpSpawnedEvent(
    Guid Id,
    string Type,
    double X,
    double Y,
    long Timestamp,
    long SentAt = 0
);

public record PowerUpCollectedEvent(
    Guid Id,
    string PlayerId,
    string Username,
    int NewLives,
    long Timestamp,
    long SentAt = 0
);

public record MqttCollisionEvent(
    string AttackerId,
    string VictimId,
    int Damage,
    long Timestamp,
    long SentAt = 0
);

public record MqttGameOverEvent(
    string WinnerId,
    string WinnerUsername,
    long Timestamp,
    long SentAt = 0
);

// ==================== Error Events ====================

public record ErrorEvent(
    string Code,
    string Message
);
