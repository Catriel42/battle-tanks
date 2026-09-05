using BattleTanks_Backend.Data;
using BattleTanks_Backend.Models.DTOs;
using BattleTanks_Backend.Models.GameState;
using BattleTanks_Backend.Services;
using BattleTanks_Backend.Services.GameEngine;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BattleTanks_Backend.Hubs;

[Authorize]
public class GameHub : Hub
{
    private readonly GameRoomManager _roomManager;
    private readonly BattleTanksDbContext _dbContext;
    private readonly EventHistoryService _historyService;
    private readonly ILogger<GameHub> _logger;
    
    public GameHub(
        GameRoomManager roomManager, 
        BattleTanksDbContext dbContext,
        EventHistoryService historyService,
        ILogger<GameHub> logger)
    {
        _roomManager = roomManager;
        _dbContext = dbContext;
        _historyService = historyService;
        _logger = logger;
    }
    
    private Guid GetPlayerId()
    {
        var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null && Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
    
    private string GetUsername()
    {
        return Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
    }
    
    public override async Task OnConnectedAsync()
    {
        var playerId = GetPlayerId();
        var username = GetUsername();
        
        _logger.LogInformation("Player {Username} ({PlayerId}) connected with ConnectionId {ConnectionId}", 
            username, playerId, Context.ConnectionId);
        
        await Clients.Caller.SendAsync("Connected", new
        {
            ConnectionId = Context.ConnectionId,
            PlayerId = playerId,
            Username = username,
            ServerTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
        
        await base.OnConnectedAsync();
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var roomId = _roomManager.GetRoomForConnection(Context.ConnectionId);
        
        if (roomId != null)
        {
            var username = GetUsername();
            
            var (success, roomDeleted, newHostId) = _roomManager.RemovePlayer(Context.ConnectionId);
            
            if (success)
            {
                if (roomDeleted)
                {
                    // Room was deleted - update database
                    if (Guid.TryParse(roomId, out var sessionId))
                    {
                        var session = await _dbContext.GameSessions.FindAsync(sessionId);
                        if (session != null)
                        {
                            session.Status = Models.Entities.GameSessionStatus.Cancelled;
                            session.FinishedAt = DateTime.UtcNow;
                            await _dbContext.SaveChangesAsync();
                            _logger.LogInformation("Room {RoomId} cancelled - all players left", roomId);
                        }
                    }
                }
                else
                {
                    var room = _roomManager.GetRoom(roomId);
                    
                    // Notify others in the room
                    await Clients.Group(roomId).SendAsync("PlayerLeft", new PlayerLeftEvent(
                        Context.ConnectionId,
                        username,
                        room?.Tanks.Count ?? 0
                    ));
                    
                    // If host changed, notify everyone
                    if (newHostId != null)
                    {
                        await Clients.Group(roomId).SendAsync("HostChanged", new HostChangedEvent(newHostId));
                    }
                    
                    // Broadcast updated room state to remaining players
                    if (room != null)
                    {
                        await BroadcastRoomState(roomId, room);
                    }
                }
                
                _logger.LogInformation("Player {Username} disconnected from room {RoomId}", username, roomId);
            }
        }
        
        await base.OnDisconnectedAsync(exception);
    }
    
    /// <summary>
    /// Join a game room. Creates the in-memory room if it doesn't exist.
    /// </summary>
    public async Task JoinRoom(string roomId)
    {
        var playerId = GetPlayerId();
        var username = GetUsername();
        
        if (playerId == Guid.Empty)
        {
            await SendError("UNAUTHORIZED", "Invalid player ID");
            return;
        }
        
        // Check if already in another room
        var currentRoom = _roomManager.GetRoomForConnection(Context.ConnectionId);
        if (currentRoom != null && currentRoom != roomId)
        {
            await SendError("ALREADY_IN_ROOM", "You are already in another room");
            return;
        }
        
        // Get or create the in-memory room
        var room = _roomManager.GetRoom(roomId);
        
        if (room == null)
        {
            // Load from database and create in-memory room
            if (!Guid.TryParse(roomId, out var sessionId))
            {
                await SendError("INVALID_ROOM", "Invalid room ID format");
                return;
            }
            
            var session = await _dbContext.GameSessions
                .Include(s => s.Map)
                .FirstOrDefaultAsync(s => s.Id == sessionId);
            
            if (session == null)
            {
                await SendError("ROOM_NOT_FOUND", "Room does not exist");
                return;
            }
            
            if (session.Status != Models.Entities.GameSessionStatus.Waiting &&
                session.Status != Models.Entities.GameSessionStatus.InProgress)
            {
                await SendError("ROOM_CLOSED", "Room is no longer accepting players");
                return;
            }
            
            var mapGrid = GameRoomManager.ParseMapData(session.Map.TileData);
            
            room = _roomManager.CreateRoom(
                sessionId,
                mapGrid,
                session.Map.Width,
                session.Map.Height,
                session.Lives,
                session.MaxPlayers,
                session.MinPlayers
            );
        }
        
        // Add player to room
        var tank = _roomManager.AddPlayer(roomId, Context.ConnectionId, playerId, username);
        
        if (tank == null)
        {
            await SendError("JOIN_FAILED", "Could not join room");
            return;
        }
        
        // Add to SignalR group
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        
        // Notify caller of successful join
        var tankDto = CreateTankDto(tank);
        await Clients.Caller.SendAsync("JoinedRoom", new
        {
            RoomId = roomId,
            Tank = tankDto,
            MapWidth = room.MapWidth,
            MapHeight = room.MapHeight,
            MapGrid = room.MapGrid,
            Lives = room.Lives,
            MaxPlayers = room.MaxPlayers,
            MinPlayers = room.MinPlayers,
            Status = room.Status.ToString().ToLowerInvariant()
        });
        
        // Notify others
        await Clients.OthersInGroup(roomId).SendAsync("PlayerJoined", new PlayerJoinedEvent(
            tankDto,
            room.Tanks.Count
        ));
        
        // Broadcast updated room state to ALL players in the room
        await BroadcastRoomState(roomId, room);
        
        _logger.LogInformation("Player {Username} joined room {RoomId}", username, roomId);
    }
    
    /// <summary>
    /// Leave the current room
    /// </summary>
    public async Task LeaveRoom()
    {
        var roomId = _roomManager.GetRoomForConnection(Context.ConnectionId);
        if (roomId == null)
        {
            await SendError("NOT_IN_ROOM", "You are not in a room");
            return;
        }
        
        var username = GetUsername();
        
        var (success, roomDeleted, newHostId) = _roomManager.RemovePlayer(Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
        
        await Clients.Caller.SendAsync("LeftRoom", new { RoomId = roomId });
        
        if (success && !roomDeleted)
        {
            var room = _roomManager.GetRoom(roomId);
            
            await Clients.Group(roomId).SendAsync("PlayerLeft", new PlayerLeftEvent(
                Context.ConnectionId,
                username,
                room?.Tanks.Count ?? 0
            ));
            
            // If host changed, notify everyone
            if (newHostId != null)
            {
                await Clients.Group(roomId).SendAsync("HostChanged", new HostChangedEvent(newHostId));
            }
            
            // Broadcast updated room state to remaining players
            if (room != null)
            {
                await BroadcastRoomState(roomId, room);
            }
        }
        
        if (roomDeleted)
        {
            // Update database
            if (Guid.TryParse(roomId, out var sessionId))
            {
                var session = await _dbContext.GameSessions.FindAsync(sessionId);
                if (session != null)
                {
                    session.Status = Models.Entities.GameSessionStatus.Cancelled;
                    session.FinishedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation("Room {RoomId} cancelled - all players left", roomId);
                }
            }
        }
        
        _logger.LogInformation("Player {Username} left room {RoomId}", username, roomId);
    }
    
    /// <summary>
    /// Start the game (host only)
    /// </summary>
    public async Task StartGame()
    {
        var roomId = _roomManager.GetRoomForConnection(Context.ConnectionId);
        if (roomId == null)
        {
            await SendError("NOT_IN_ROOM", "You are not in a room");
            return;
        }
        
        var room = _roomManager.GetRoom(roomId);
        if (room == null)
        {
            await SendError("ROOM_NOT_FOUND", "Room not found");
            return;
        }
        
        // Check if caller is the host
        if (!_roomManager.IsHost(Context.ConnectionId))
        {
            await SendError("NOT_HOST", "Only the host can start the game");
            return;
        }
        
        if (room.Tanks.Count < room.MinPlayers)
        {
            await SendError("NOT_ENOUGH_PLAYERS", $"Need at least {room.MinPlayers} players");
            return;
        }
        
        if (!_roomManager.StartGame(roomId))
        {
            await SendError("START_FAILED", "Could not start the game");
            return;
        }
        
        // Update database
        if (Guid.TryParse(roomId, out var sessionId))
        {
            var session = await _dbContext.GameSessions.FindAsync(sessionId);
            if (session != null)
            {
                session.Status = Models.Entities.GameSessionStatus.InProgress;
                session.StartedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
            }
        }
        
        _logger.LogInformation("Game starting in room {RoomId} by host", roomId);
    }
    
    /// <summary>
    /// Send player input (move/shoot)
    /// </summary>
    public async Task SendInput(PlayerInputDto input)
    {
        var roomId = _roomManager.GetRoomForConnection(Context.ConnectionId);
        if (roomId == null)
        {
            return; // Silently ignore inputs when not in a room
        }
        
        var room = _roomManager.GetRoom(roomId);
        if (room == null || room.Status != GameStatus.Playing)
        {
            return; // Silently ignore inputs when game not playing
        }
        
        // Parse input type
        var inputType = input.Type.ToLowerInvariant() switch
        {
            "move_start" => InputType.MoveStart,
            "move_stop" => InputType.MoveStop,
            "shoot" => InputType.Shoot,
            _ => (InputType?)null
        };
        
        if (!inputType.HasValue)
        {
            return; // Invalid input type
        }
        
        // Parse direction for movement
        Direction? direction = null;
        if (inputType == InputType.MoveStart && !string.IsNullOrEmpty(input.Direction))
        {
            direction = input.Direction.ToLowerInvariant() switch
            {
                "up" => Direction.Up,
                "down" => Direction.Down,
                "left" => Direction.Left,
                "right" => Direction.Right,
                _ => null
            };
            
            if (!direction.HasValue)
            {
                return; // Invalid direction
            }
        }
        
        var playerInput = new PlayerInput
        {
            ConnectionId = Context.ConnectionId,
            Type = inputType.Value,
            Direction = direction,
            SequenceNumber = input.SequenceNumber
        };
        
        _roomManager.QueueInput(Context.ConnectionId, playerInput);
    }
    
    /// <summary>
    /// Ping for latency measurement
    /// </summary>
    public async Task Ping()
    {
        await Clients.Caller.SendAsync("Pong", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }
    
    /// <summary>
    /// Send a chat message to all players in the room
    /// </summary>
    public async Task SendChatMessage(ChatMessageDto message)
    {
        var roomId = _roomManager.GetRoomForConnection(Context.ConnectionId);
        if (roomId == null)
        {
            await SendError("NOT_IN_ROOM", "You are not in a room");
            return;
        }
        
        var username = GetUsername();
        var text = message.Text?.Trim();
        
        if (string.IsNullOrEmpty(text) || text.Length > 500)
        {
            return; // Ignore empty or too long messages
        }
        
        var chatEvent = new ChatMessageEvent(
            username,
            text,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        );
        
        await Clients.Group(roomId).SendAsync("ChatMessage", chatEvent);
        
        _logger.LogDebug("Chat in room {RoomId}: {Username}: {Text}", roomId, username, text);
    }
    
    /// <summary>
    /// Get current room state
    /// </summary>
    public async Task GetRoomState()
    {
        var roomId = _roomManager.GetRoomForConnection(Context.ConnectionId);
        if (roomId == null)
        {
            await SendError("NOT_IN_ROOM", "You are not in a room");
            return;
        }
        
        var room = _roomManager.GetRoom(roomId);
        if (room == null)
        {
            await SendError("ROOM_NOT_FOUND", "Room not found");
            return;
        }
        
        await SendRoomState(roomId, room);
    }
    
    private async Task SendRoomState(string roomId, GameRoomState room)
    {
        var players = room.Tanks.Values.Select(t => new PlayerInfoDto(
            t.PlayerId,
            t.Username,
            t.ConnectionId == room.HostConnectionId,
            true // Ready status not implemented yet
        )).ToArray();
        
        await Clients.Caller.SendAsync("RoomState", new RoomStateEvent(
            room.SessionId,
            room.Status.ToString().ToLowerInvariant(),
            players,
            room.MaxPlayers,
            room.MinPlayers,
            room.Tanks.Count >= room.MinPlayers,
            room.HostConnectionId
        ));
    }
    
    private async Task BroadcastRoomState(string roomId, GameRoomState room)
    {
        var players = room.Tanks.Values.Select(t => new PlayerInfoDto(
            t.PlayerId,
            t.Username,
            t.ConnectionId == room.HostConnectionId,
            true // Ready status not implemented yet
        )).ToArray();
        
        await Clients.Group(roomId).SendAsync("RoomState", new RoomStateEvent(
            room.SessionId,
            room.Status.ToString().ToLowerInvariant(),
            players,
            room.MaxPlayers,
            room.MinPlayers,
            room.Tanks.Count >= room.MinPlayers,
            room.HostConnectionId
        ));
    }
    
    private async Task SendError(string code, string message)
    {
        await Clients.Caller.SendAsync("Error", new ErrorEvent(code, message));
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
    
    public async Task GetEventHistory()
    {
        var roomId = _roomManager.GetRoomForConnection(Context.ConnectionId);
        if (roomId == null)
        {
            await SendError("NOT_IN_ROOM", "You are not in a room");
            return;
        }
        
        var history = await _historyService.GetHistoryAsync(roomId, count: 50);
        
        await Clients.Caller.SendAsync("EventHistory", new
        {
            RoomId = roomId,
            Events = history,
            ReceivedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
        
        _logger.LogDebug("Retrieved event history for room {RoomId} ({EventCount} events)", roomId, history.Count);
    }
}
