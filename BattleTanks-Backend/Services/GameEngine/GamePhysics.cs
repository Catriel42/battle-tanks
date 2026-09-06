using BattleTanks_Backend.Models.DTOs;
using BattleTanks_Backend.Models.GameState;
using System.Collections.Concurrent;

namespace BattleTanks_Backend.Services.GameEngine;

public class GamePhysics
{
    private readonly GameRoomState _room;
    private readonly PowerUpManager _powerUpManager;
    private readonly ConcurrentQueue<PlayerInput> _inputQueue = new();
    
    // Events that occurred during the last tick
    public List<PlayerHitEvent> HitEvents { get; } = [];
    public List<PlayerKilledEvent> KillEvents { get; } = [];
    public List<PlayerEliminatedEvent> EliminationEvents { get; } = [];
    public List<BlockDestroyedEvent> BlockDestroyedEvents { get; } = [];
    public List<BulletFiredEvent> BulletFiredEvents { get; } = [];
    public List<BulletHitEvent> BulletHitEvents { get; } = [];
    public List<PlayerRespawnedEvent> RespawnEvents { get; } = [];
    
    // Power-up events
    public List<PowerUpSpawnedEvent> PowerUpSpawnedEvents => _powerUpManager.SpawnedEvents;
    public List<PowerUpCollectedEvent> PowerUpCollectedEvents => _powerUpManager.CollectedEvents;
    
    public GamePhysics(GameRoomState room)
    {
        _room = room;
        _powerUpManager = new PowerUpManager(room);
    }
    
    public void QueueInput(PlayerInput input)
    {
        if (_inputQueue.Count < GameConstants.MaxInputQueueSize)
        {
            _inputQueue.Enqueue(input);
        }
    }
    
    public void Tick()
    {
        if (_room.Status != GameStatus.Playing)
            return;
        
        ClearEvents();
        
        ProcessInputs();
        
        UpdateTanks();
        
        UpdateBullets();
        
        _powerUpManager.TrySpawnPowerUp();
        
        CheckWinCondition();
        
        _room.CurrentTick++;
    }
    
    private void ClearEvents()
    {
        HitEvents.Clear();
        KillEvents.Clear();
        EliminationEvents.Clear();
        BlockDestroyedEvents.Clear();
        BulletFiredEvents.Clear();
        BulletHitEvents.Clear();
        RespawnEvents.Clear();
        _powerUpManager.ClearEvents();
    }
    
    private void ProcessInputs()
    {
        while (_inputQueue.TryDequeue(out var input))
        {
            if (!_room.Tanks.TryGetValue(input.ConnectionId, out var tank))
                continue;
            
            if (!tank.IsAlive)
                continue;
            
            switch (input.Type)
            {
                case InputType.MoveStart:
                    if (input.Direction.HasValue)
                    {
                        tank.IsMoving = true;
                        tank.MoveDirection = input.Direction.Value;
                        tank.Direction = input.Direction.Value;
                    }
                    break;
                    
                case InputType.MoveStop:
                    tank.IsMoving = false;
                    tank.MoveDirection = null;
                    break;
                    
                case InputType.Shoot:
                    TryShoot(tank);
                    break;
            }
        }
    }
    
    private void TryShoot(TankState tank)
    {
        if (!tank.CanShoot(GameConstants.ShootCooldownMs))
            return;
        
        tank.LastShotTime = DateTime.UtcNow;
        tank.ShotsFired++;
        
        // Calculate bullet spawn position (center of tank, offset in direction)
        var tankCenterX = tank.X + GameConstants.TankSize / 2.0;
        var tankCenterY = tank.Y + GameConstants.TankSize / 2.0;
        
        var (dx, dy) = tank.Direction.ToVector();
        var bulletX = tankCenterX + dx * (GameConstants.TankSize / 2.0 + GameConstants.BulletSize / 2.0) - GameConstants.BulletSize / 2.0;
        var bulletY = tankCenterY + dy * (GameConstants.TankSize / 2.0 + GameConstants.BulletSize / 2.0) - GameConstants.BulletSize / 2.0;
        
        var bullet = new BulletState
        {
            Id = Guid.NewGuid().ToString(),
            OwnerId = tank.ConnectionId,
            X = bulletX,
            Y = bulletY,
            Direction = tank.Direction,
            Speed = GameConstants.BulletSpeed,
            Damage = GameConstants.BulletDamage
        };
        
        _room.Bullets.Add(bullet);
        
        BulletFiredEvents.Add(new BulletFiredEvent(new BulletDto(
            bullet.Id,
            bullet.OwnerId,
            bullet.X,
            bullet.Y,
            bullet.Direction.ToString().ToLowerInvariant()
        )));
    }
    
    private void UpdateTanks()
    {
        foreach (var (_, tank) in _room.Tanks)
        {
            if (!tank.IsAlive || !tank.IsMoving || !tank.MoveDirection.HasValue)
                continue;
            
            var (newX, newY, moved) = CollisionDetector.TryMoveTank(tank, tank.MoveDirection.Value, _room);
            
            if (moved)
            {
                tank.X = newX;
                tank.Y = newY;
            }
            
            _powerUpManager.CheckCollection(tank);
        }
    }
    
    private void UpdateBullets()
    {
        var bulletsToRemove = new List<BulletState>();
        var now = DateTime.UtcNow;
        
        foreach (var bullet in _room.Bullets)
        {
            // Check bullet lifetime
            if ((now - bullet.CreatedAt).TotalMilliseconds > GameConstants.MaxBulletLifetimeMs)
            {
                bulletsToRemove.Add(bullet);
                continue;
            }
            
            // Move bullet
            bullet.Update();
            
            // Check if out of bounds
            if (bullet.IsOutOfBounds(_room.MapWidth * GameConstants.TileSize, _room.MapHeight * GameConstants.TileSize))
            {
                bulletsToRemove.Add(bullet);
                BulletHitEvents.Add(new BulletHitEvent(bullet.Id, null, null, null));
                continue;
            }
            
            // Check tile collision
            var tileHit = CollisionDetector.BulletHitsTile(bullet.X, bullet.Y, _room);
            if (tileHit.HasValue)
            {
                var (row, col) = tileHit.Value;
                bulletsToRemove.Add(bullet);
                
                // Destroy brick blocks
                if (_room.IsTileDestructible(row, col))
                {
                    _room.DestroyBlock(row, col);
                    
                    if (_room.Tanks.TryGetValue(bullet.OwnerId, out var shooter))
                    {
                        shooter.BlocksDestroyed++;
                    }
                    
                    BlockDestroyedEvents.Add(new BlockDestroyedEvent(row, col, bullet.OwnerId));
                }
                
                BulletHitEvents.Add(new BulletHitEvent(bullet.Id, null, row, col));
                continue;
            }
            
            // Check tank collision
            var tankHit = CollisionDetector.BulletHitsTank(bullet.X, bullet.Y, bullet.OwnerId, _room);
            if (tankHit != null)
            {
                bulletsToRemove.Add(bullet);
                
                // Apply damage
                var previousHealth = tankHit.Health;
                var previousLives = tankHit.Lives;
                tankHit.TakeDamage(bullet.Damage);
                
                // Update shooter stats
                if (_room.Tanks.TryGetValue(bullet.OwnerId, out var shooter))
                {
                    shooter.ShotsHit++;
                    shooter.DamageDealt += bullet.Damage;
                }
                
                // Create hit event
                HitEvents.Add(new PlayerHitEvent(
                    tankHit.ConnectionId,
                    bullet.OwnerId,
                    bullet.Damage,
                    tankHit.Health,
                    tankHit.Lives
                ));
                
                BulletHitEvents.Add(new BulletHitEvent(bullet.Id, tankHit.ConnectionId, null, null));
                
                // Check if killed
                if (tankHit.Health <= 0 && previousHealth > 0)
                {
                    var shooterName = shooter?.Username ?? "Unknown";
                    
                    if (shooter != null)
                    {
                        shooter.Kills++;
                    }
                    
                    KillEvents.Add(new PlayerKilledEvent(
                        tankHit.ConnectionId,
                        tankHit.Username,
                        bullet.OwnerId,
                        shooterName,
                        tankHit.Lives
                    ));
                    
                    // Check if eliminated (no lives left)
                    if (tankHit.Lives <= 0 && previousLives > 0)
                    {
                        _room.EliminationOrder.Add(tankHit.ConnectionId);
                        var alivePlayers = _room.GetAlivePlayersCount();
                        var position = _room.Tanks.Count - _room.EliminationOrder.Count + 1;
                        
                        EliminationEvents.Add(new PlayerEliminatedEvent(
                            tankHit.ConnectionId,
                            tankHit.Username,
                            position,
                            alivePlayers
                        ));
                    }
                    else if (tankHit.Lives > 0)
                    {
                        // Respawn the player
                        tankHit.Respawn();
                        RespawnEvents.Add(new PlayerRespawnedEvent(
                            tankHit.ConnectionId,
                            tankHit.X,
                            tankHit.Y
                        ));
                    }
                }
            }
        }
        
        // Remove bullets that hit something
        foreach (var bullet in bulletsToRemove)
        {
            _room.RemoveBullet(bullet);
        }
    }
    
    private void CheckWinCondition()
    {
        var alivePlayers = _room.Tanks.Values.Where(t => !t.IsEliminated).ToList();
        
        if (alivePlayers.Count <= 1)
        {
            _room.Status = GameStatus.Finished;
        }
    }
    
    /// <summary>
    /// Create a snapshot of the current game state for broadcasting
    /// </summary>
    public GameStateSnapshot CreateSnapshot()
    {
        var tanks = _room.Tanks.Values.Select(t => new TankDto(
            t.ConnectionId,
            t.PlayerId,
            t.Username,
            t.X,
            t.Y,
            t.Direction.ToString().ToLowerInvariant(),
            t.IsMoving,
            t.Health,
            t.Lives,
            t.IsAlive,
            t.IsEliminated
        )).ToArray();
        
        var bullets = _room.Bullets.Select(b => new BulletDto(
            b.Id,
            b.OwnerId,
            b.X,
            b.Y,
            b.Direction.ToString().ToLowerInvariant()
        )).ToArray();
        
        // Flatten destroyed blocks: [row1, col1, row2, col2, ...]
        var destroyedBlocks = _room.DestroyedBlocks
            .SelectMany(b => new[] { b.row, b.col })
            .ToArray();
        
        return new GameStateSnapshot(
            _room.CurrentTick,
            _room.Status.ToString().ToLowerInvariant(),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            tanks,
            bullets,
            destroyedBlocks
        );
    }
}
