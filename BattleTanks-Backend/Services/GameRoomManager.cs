using BattleTanks_Backend.Models.GameState;
using BattleTanks_Backend.Services.GameEngine;
using System.Collections.Concurrent;
using System.Text.Json;

namespace BattleTanks_Backend.Services;

/// <summary>
/// Manages all active game rooms in memory.
/// Thread-safe for concurrent access from SignalR hub and game loop.
/// </summary>
public class GameRoomManager
{
    private readonly ConcurrentDictionary<string, GameRoomState> _rooms = new();
    private readonly ConcurrentDictionary<string, GamePhysics> _physics = new();
    private readonly ConcurrentDictionary<string, string> _connectionToRoom = new();
    
    private readonly ILogger<GameRoomManager> _logger;
    
    public GameRoomManager(ILogger<GameRoomManager> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Create a new game room from a database GameSession
    /// </summary>
    public GameRoomState CreateRoom(Guid sessionId, int[][] mapGrid, int mapWidth, int mapHeight, int lives, int maxPlayers, int minPlayers)
    {
        var roomId = sessionId.ToString();
        
        var room = new GameRoomState
        {
            RoomId = roomId,
            SessionId = sessionId,
            MapGrid = mapGrid,
            MapWidth = mapWidth,
            MapHeight = mapHeight,
            Lives = lives,
            MaxPlayers = maxPlayers,
            MinPlayers = minPlayers,
            Status = GameStatus.Waiting
        };
        
        room.InitializeSpawnPoints();
        
        var physics = new GamePhysics(room);
        
        if (_rooms.TryAdd(roomId, room) && _physics.TryAdd(roomId, physics))
        {
            _logger.LogInformation("Created game room {RoomId} with map {Width}x{Height}", roomId, mapWidth, mapHeight);
            return room;
        }
        
        throw new InvalidOperationException($"Room {roomId} already exists");
    }
    
    /// <summary>
    /// Get a room by its ID
    /// </summary>
    public GameRoomState? GetRoom(string roomId)
    {
        _rooms.TryGetValue(roomId, out var room);
        return room;
    }
    
    /// <summary>
    /// Get the physics engine for a room
    /// </summary>
    public GamePhysics? GetPhysics(string roomId)
    {
        _physics.TryGetValue(roomId, out var physics);
        return physics;
    }
    
    /// <summary>
    /// Get the room a connection is in
    /// </summary>
    public string? GetRoomForConnection(string connectionId)
    {
        _connectionToRoom.TryGetValue(connectionId, out var roomId);
        return roomId;
    }
    
    /// <summary>
    /// Add a player to a room
    /// </summary>
    public TankState? AddPlayer(string roomId, string connectionId, Guid playerId, string username)
    {
        if (!_rooms.TryGetValue(roomId, out var room))
        {
            _logger.LogWarning("Cannot add player to non-existent room {RoomId}", roomId);
            return null;
        }
        
        if (room.Status != GameStatus.Waiting)
        {
            _logger.LogWarning("Cannot add player to room {RoomId} - game already started", roomId);
            return null;
        }
        
        if (room.Tanks.Count >= room.MaxPlayers)
        {
            _logger.LogWarning("Cannot add player to room {RoomId} - room is full", roomId);
            return null;
        }
        
        // Check if player is already in the room
        if (room.Tanks.ContainsKey(connectionId))
        {
            _logger.LogWarning("Player {ConnectionId} already in room {RoomId}", connectionId, roomId);
            return room.Tanks[connectionId];
        }
        
        var playerIndex = room.Tanks.Count;
        var (spawnX, spawnY) = room.GetSpawnPoint(playerIndex);
        
        var tank = new TankState
        {
            ConnectionId = connectionId,
            PlayerId = playerId,
            Username = username,
            X = spawnX,
            Y = spawnY,
            SpawnX = spawnX,
            SpawnY = spawnY,
            Lives = room.Lives,
            Health = GameConstants.TankMaxHealth
        };
        
        if (room.Tanks.TryAdd(connectionId, tank))
        {
            _connectionToRoom[connectionId] = roomId;
            
            // First player becomes the host
            if (room.HostConnectionId == null)
            {
                room.HostConnectionId = connectionId;
                _logger.LogInformation("Player {Username} is now host of room {RoomId}", username, roomId);
            }
            
            _logger.LogInformation("Player {Username} joined room {RoomId} at position ({X}, {Y})", 
                username, roomId, spawnX, spawnY);
            return tank;
        }
        
        return null;
    }
    
    /// <summary>
    /// Remove a player from their current room.
    /// Returns (success, roomWasDeleted, newHostConnectionId)
    /// </summary>
    public (bool success, bool roomDeleted, string? newHostId) RemovePlayer(string connectionId)
    {
        if (!_connectionToRoom.TryRemove(connectionId, out var roomId))
        {
            return (false, false, null);
        }
        
        if (!_rooms.TryGetValue(roomId, out var room))
        {
            return (false, false, null);
        }
        
        if (room.Tanks.TryRemove(connectionId, out var tank))
        {
            _logger.LogInformation("Player {Username} left room {RoomId}", tank.Username, roomId);
            
            // If room is empty and game hasn't started, remove it
            if (room.Tanks.IsEmpty && room.Status == GameStatus.Waiting)
            {
                RemoveRoom(roomId);
                return (true, true, null);
            }
            
            // If the host left and game hasn't started, transfer host to next player
            string? newHostId = null;
            if (room.HostConnectionId == connectionId && room.Status == GameStatus.Waiting)
            {
                var nextPlayer = room.Tanks.Values.FirstOrDefault();
                if (nextPlayer != null)
                {
                    room.HostConnectionId = nextPlayer.ConnectionId;
                    newHostId = nextPlayer.ConnectionId;
                    _logger.LogInformation("Host transferred to {Username} in room {RoomId}", 
                        nextPlayer.Username, roomId);
                }
            }
            
            return (true, false, newHostId);
        }
        
        return (false, false, null);
    }
    
    /// <summary>
    /// Check if a connection is the host of their room
    /// </summary>
    public bool IsHost(string connectionId)
    {
        var roomId = GetRoomForConnection(connectionId);
        if (roomId == null) return false;
        
        var room = GetRoom(roomId);
        return room?.HostConnectionId == connectionId;
    }
    
    /// <summary>
    /// Get the host connection ID for a room
    /// </summary>
    public string? GetHostConnectionId(string roomId)
    {
        var room = GetRoom(roomId);
        return room?.HostConnectionId;
    }
    
    /// <summary>
    /// Start the game in a room
    /// </summary>
    public bool StartGame(string roomId)
    {
        if (!_rooms.TryGetValue(roomId, out var room))
        {
            return false;
        }
        
        if (room.Status != GameStatus.Waiting)
        {
            return false;
        }
        
        if (room.Tanks.Count < room.MinPlayers)
        {
            return false;
        }
        
        room.Status = GameStatus.Starting;
        _logger.LogInformation("Game starting in room {RoomId} with {PlayerCount} players", 
            roomId, room.Tanks.Count);
        
        return true;
    }
    
    /// <summary>
    /// Transition from Starting to Playing state
    /// </summary>
    public bool BeginPlaying(string roomId)
    {
        if (!_rooms.TryGetValue(roomId, out var room))
        {
            return false;
        }
        
        if (room.Status != GameStatus.Starting)
        {
            return false;
        }
        
        room.Status = GameStatus.Playing;
        room.StartedAt = DateTime.UtcNow;
        room.CurrentTick = 0;
        
        _logger.LogInformation("Game playing in room {RoomId}", roomId);
        
        return true;
    }
    
    /// <summary>
    /// End the game in a room
    /// </summary>
    public void EndGame(string roomId)
    {
        if (_rooms.TryGetValue(roomId, out var room))
        {
            room.Status = GameStatus.Finished;
            _logger.LogInformation("Game ended in room {RoomId}", roomId);
        }
    }
    
    /// <summary>
    /// Remove a room completely
    /// </summary>
    public void RemoveRoom(string roomId)
    {
        _rooms.TryRemove(roomId, out _);
        _physics.TryRemove(roomId, out _);
        
        // Remove all connection mappings for this room
        var connectionsToRemove = _connectionToRoom
            .Where(kvp => kvp.Value == roomId)
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var connectionId in connectionsToRemove)
        {
            _connectionToRoom.TryRemove(connectionId, out _);
        }
        
        _logger.LogInformation("Removed room {RoomId}", roomId);
    }
    
    /// <summary>
    /// Get all active rooms (for game loop)
    /// </summary>
    public IEnumerable<(string RoomId, GameRoomState Room, GamePhysics Physics)> GetActiveRooms()
    {
        foreach (var (roomId, room) in _rooms)
        {
            if (room.Status == GameStatus.Playing && _physics.TryGetValue(roomId, out var physics))
            {
                yield return (roomId, room, physics);
            }
        }
    }
    
    /// <summary>
    /// Get rooms in Starting state (for countdown)
    /// </summary>
    public IEnumerable<(string RoomId, GameRoomState Room)> GetStartingRooms()
    {
        foreach (var (roomId, room) in _rooms)
        {
            if (room.Status == GameStatus.Starting)
            {
                yield return (roomId, room);
            }
        }
    }
    
    /// <summary>
    /// Queue an input for processing in the next tick
    /// </summary>
    public void QueueInput(string connectionId, PlayerInput input)
    {
        var roomId = GetRoomForConnection(connectionId);
        if (roomId == null) return;
        
        var physics = GetPhysics(roomId);
        physics?.QueueInput(input);
    }
    
    /// <summary>
    /// Parse map tile data from JSON string
    /// </summary>
    public static int[][] ParseMapData(string tileDataJson)
    {
        return JsonSerializer.Deserialize<int[][]>(tileDataJson) ?? [];
    }
}
