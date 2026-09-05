using BattleTanks_Backend.Data;
using BattleTanks_Backend.Hubs;
using BattleTanks_Backend.Models.DTOs;
using BattleTanks_Backend.Models.Entities;
using BattleTanks_Backend.Models.GameState;
using BattleTanks_Backend.Services.GameEngine;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace BattleTanks_Backend.Services;

public class GameLoopService : BackgroundService
{
    private readonly GameRoomManager _roomManager;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MqttPublisherService _mqttPublisher;
    private readonly ILogger<GameLoopService> _logger;
    
    private readonly Dictionary<string, int> _countdowns = new();
    private readonly Dictionary<string, DateTime> _countdownStartTimes = new();
    
    public GameLoopService(
        GameRoomManager roomManager,
        IHubContext<GameHub> hubContext,
        IServiceScopeFactory scopeFactory,
        MqttPublisherService mqttPublisher,
        ILogger<GameLoopService> logger)
    {
        _roomManager = roomManager;
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
        _mqttPublisher = mqttPublisher;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Game loop service starting at {TickRate} ticks/second", GameConstants.TickRate);
        
        var stopwatch = new Stopwatch();
        var targetTickTime = TimeSpan.FromMilliseconds(GameConstants.TickDurationMs);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            stopwatch.Restart();
            
            try
            {
                // Process countdowns for starting games
                await ProcessCountdowns();
                
                // Process all active games
                await ProcessActiveGames();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in game loop tick");
            }
            
            stopwatch.Stop();
            
            // Sleep to maintain tick rate
            var elapsed = stopwatch.Elapsed;
            if (elapsed < targetTickTime)
            {
                await Task.Delay(targetTickTime - elapsed, stoppingToken);
            }
            else if (elapsed > targetTickTime * 2)
            {
                _logger.LogWarning("Game loop tick took {Elapsed}ms, expected {Target}ms", 
                    elapsed.TotalMilliseconds, GameConstants.TickDurationMs);
            }
        }
        
        _logger.LogInformation("Game loop service stopped");
    }
    
    private async Task ProcessCountdowns()
    {
        foreach (var (roomId, room) in _roomManager.GetStartingRooms())
        {
            if (!_countdownStartTimes.TryGetValue(roomId, out var startTime))
            {
                // Start countdown
                _countdownStartTimes[roomId] = DateTime.UtcNow;
                _countdowns[roomId] = GameConstants.CountdownSeconds;
                
                // Send initial countdown event
                var tanks = room.Tanks.Values.Select(CreateTankDto).ToArray();
                await _hubContext.Clients.Group(roomId).SendAsync("GameStarting", new GameStartingEvent(
                    GameConstants.CountdownSeconds,
                    tanks,
                    room.MapWidth,
                    room.MapHeight,
                    room.MapGrid
                ));
                
                _logger.LogInformation("Countdown started for room {RoomId}", roomId);
                continue;
            }
            
            var elapsed = DateTime.UtcNow - startTime;
            var remainingSeconds = GameConstants.CountdownSeconds - (int)elapsed.TotalSeconds;
            
            if (remainingSeconds != _countdowns[roomId])
            {
                _countdowns[roomId] = remainingSeconds;
                
                if (remainingSeconds > 0)
                {
                    // Send countdown update
                    await _hubContext.Clients.Group(roomId).SendAsync("CountdownUpdate", remainingSeconds);
                }
            }
            
            if (remainingSeconds <= 0)
            {
                // Start the game
                _roomManager.BeginPlaying(roomId);
                _countdownStartTimes.Remove(roomId);
                _countdowns.Remove(roomId);
                
                var tanks = room.Tanks.Values.Select(CreateTankDto).ToArray();
                await _hubContext.Clients.Group(roomId).SendAsync("GameStarted", new GameStartedEvent(
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    tanks
                ));
                
                _logger.LogInformation("Game started in room {RoomId}", roomId);
            }
        }
    }
    
    private async Task ProcessActiveGames()
    {
        foreach (var (roomId, room, physics) in _roomManager.GetActiveRooms())
        {
            // Run physics tick
            physics.Tick();
            
            // Broadcast events
            await BroadcastEvents(roomId, physics);
            
            // Broadcast state snapshot every N ticks
            if (room.CurrentTick % GameConstants.StateSnapshotIntervalTicks == 0)
            {
                var snapshot = physics.CreateSnapshot();
                await _hubContext.Clients.Group(roomId).SendAsync("GameState", snapshot);
            }
            
            // Check if game is over
            if (room.Status == GameStatus.Finished)
            {
                await HandleGameOver(roomId, room);
            }
        }
    }
    
    private async Task BroadcastEvents(string roomId, GamePhysics physics)
    {
        var group = _hubContext.Clients.Group(roomId);
        
        foreach (var hit in physics.HitEvents)
        {
            await group.SendAsync("PlayerHit", hit);
        }
        
        foreach (var kill in physics.KillEvents)
        {
            await group.SendAsync("PlayerKilled", kill);
        }
        
        foreach (var elimination in physics.EliminationEvents)
        {
            await group.SendAsync("PlayerEliminated", elimination);
        }
        
        foreach (var respawn in physics.RespawnEvents)
        {
            await group.SendAsync("PlayerRespawned", respawn);
        }
        
        foreach (var block in physics.BlockDestroyedEvents)
        {
            await group.SendAsync("BlockDestroyed", block);
        }
        
        foreach (var bullet in physics.BulletFiredEvents)
        {
            await group.SendAsync("BulletFired", bullet);
        }
        
        foreach (var powerUpEvent in physics.PowerUpSpawnedEvents)
        {
            await _mqttPublisher.PublishPowerUpSpawnedAsync(
                roomId,
                powerUpEvent.Id,
                powerUpEvent.Type,
                powerUpEvent.X,
                powerUpEvent.Y,
                powerUpEvent.Timestamp
            );
        }
        
        foreach (var powerUpEvent in physics.PowerUpCollectedEvents)
        {
            await _mqttPublisher.PublishPowerUpCollectedAsync(
                roomId,
                powerUpEvent.Id,
                powerUpEvent.PlayerId,
                powerUpEvent.Username,
                powerUpEvent.NewLives,
                powerUpEvent.Timestamp
            );
        }
    }
    
    private async Task HandleGameOver(string roomId, GameRoomState room)
    {
        var winner = room.GetWinner();
        var durationMs = room.StartedAt.HasValue 
            ? (long)(DateTime.UtcNow - room.StartedAt.Value).TotalMilliseconds 
            : 0;
        
        // Build final stats sorted by elimination order (winner first)
        var allPlayers = room.Tanks.Values.ToList();
        var finalStats = new List<PlayerStatsDto>();
        
        // Winner is position 1
        if (winner != null)
        {
            finalStats.Add(CreatePlayerStatsDto(winner, 1));
            allPlayers.Remove(winner);
        }
        
        // Rest are ordered by elimination (last eliminated = 2nd place)
        var eliminatedInOrder = room.EliminationOrder
            .Select(connId => room.Tanks.GetValueOrDefault(connId))
            .Where(t => t != null)
            .Reverse()
            .ToList();
        
        var position = 2;
        foreach (var tank in eliminatedInOrder)
        {
            if (tank != null)
            {
                finalStats.Add(CreatePlayerStatsDto(tank, position++));
            }
        }
        
        var gameOverEvent = new GameOverEvent(
            winner?.ConnectionId,
            winner?.Username,
            finalStats.ToArray(),
            durationMs
        );
        
        await _hubContext.Clients.Group(roomId).SendAsync("GameOver", gameOverEvent);
        
        // Persist stats to database
        await PersistGameStats(room, winner, durationMs);
        
        _logger.LogInformation("Game over in room {RoomId}. Winner: {Winner}", 
            roomId, winner?.Username ?? "None");
        
        // Schedule room removal after a delay
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30));
            _roomManager.RemoveRoom(roomId);
        });
    }
    
    private async Task PersistGameStats(GameRoomState room, TankState? winner, long durationMs)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BattleTanksDbContext>();
            
            // Update GameSession
            var session = await dbContext.GameSessions
                .FirstOrDefaultAsync(s => s.Id == room.SessionId);
            
            if (session == null)
            {
                _logger.LogWarning("GameSession {SessionId} not found for stats persistence", room.SessionId);
                return;
            }
            
            session.Status = GameSessionStatus.Finished;
            session.WinnerId = winner?.PlayerId;
            session.DurationMs = durationMs;
            
            // Create PlayerGameStats for each player
            var position = 1;
            var orderedPlayers = new List<TankState>();
            
            if (winner != null)
            {
                orderedPlayers.Add(winner);
            }
            
            // Add eliminated players in reverse order
            foreach (var connId in room.EliminationOrder.AsEnumerable().Reverse())
            {
                if (room.Tanks.TryGetValue(connId, out var tank) && tank != winner)
                {
                    orderedPlayers.Add(tank);
                }
            }
            
            foreach (var tank in orderedPlayers)
            {
                var accuracy = tank.ShotsFired > 0 
                    ? (double)tank.ShotsHit / tank.ShotsFired * 100 
                    : 0;
                
                var stats = new PlayerGameStats
                {
                    Id = Guid.NewGuid(),
                    PlayerId = tank.PlayerId,
                    GameSessionId = room.SessionId,
                    FinalPosition = position++,
                    Kills = tank.Kills,
                    Deaths = tank.Deaths,
                    ShotsFired = tank.ShotsFired,
                    ShotsHit = tank.ShotsHit,
                    Accuracy = accuracy,
                    BlocksDestroyed = tank.BlocksDestroyed,
                    DamageDealt = tank.DamageDealt,
                    DamageTaken = tank.DamageTaken,
                    SurvivalTimeMs = tank.SurvivalTimeMs
                };
                
                dbContext.PlayerGameStats.Add(stats);
                
                // Update player aggregate stats
                var player = await dbContext.Players.FindAsync(tank.PlayerId);
                if (player != null)
                {
                    player.GamesPlayed++;
                    player.TotalKills += tank.Kills;
                    player.TotalDeaths += tank.Deaths;
                    
                    if (tank == winner)
                    {
                        player.Wins++;
                    }
                }
            }
            
            await dbContext.SaveChangesAsync();
            _logger.LogInformation("Persisted game stats for session {SessionId}", room.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist game stats for session {SessionId}", room.SessionId);
        }
    }
    
    private static TankDto CreateTankDto(TankState tank)
    {
        return new TankDto(
            tank.ConnectionId,
            tank.PlayerId,
            tank.Username,
            tank.X,
            tank.Y,
            tank.Direction.ToString().ToLowerInvariant(),
            tank.IsMoving,
            tank.Health,
            tank.Lives,
            tank.IsAlive,
            tank.IsEliminated
        );
    }
    
    private static PlayerStatsDto CreatePlayerStatsDto(TankState tank, int position)
    {
        var accuracy = tank.ShotsFired > 0 
            ? (double)tank.ShotsHit / tank.ShotsFired * 100 
            : 0;
        
        return new PlayerStatsDto(
            tank.PlayerId,
            tank.Username,
            position,
            tank.Kills,
            tank.Deaths,
            tank.ShotsFired,
            tank.ShotsHit,
            accuracy,
            tank.BlocksDestroyed,
            tank.DamageDealt,
            tank.DamageTaken,
            tank.SurvivalTimeMs
        );
    }
}
