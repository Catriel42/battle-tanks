using BattleTanks_Backend.Data;
using BattleTanks_Backend.Models.DTOs;
using BattleTanks_Backend.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BattleTanks_Backend.Controllers;

[ApiController]
[Route("api/room")]
[Authorize]
public class RoomController : ControllerBase
{
    private readonly BattleTanksDbContext _context;

    public RoomController(BattleTanksDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetRooms()
    {
        var rooms = await _context.GameSessions
            .AsNoTracking()
            .Include(r => r.Players)
            .Include(r => r.Map)
            .Where(r => r.Status == GameSessionStatus.Waiting)
            .Select(r => new RoomResponse(
                r.Id, 
                r.Status, 
                r.MapId,
                r.Map.Name,
                r.MaxPlayers,
                r.MinPlayers,
                r.Lives,
                r.Players.Count,
                r.Players.Count >= r.MinPlayers
            ))
            .ToListAsync();

        return Ok(rooms);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetRoom(Guid id)
    {
        var room = await _context.GameSessions
            .AsNoTracking()
            .Include(r => r.Players)
            .Include(r => r.Map)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (room == null) return NotFound("Room not found.");

        return Ok(new RoomResponse(
            room.Id,
            room.Status,
            room.MapId,
            room.Map.Name,
            room.MaxPlayers,
            room.MinPlayers,
            room.Lives,
            room.Players.Count,
            room.Players.Count >= room.MinPlayers
        ));
    }

    [HttpPost]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request)
    {
        var playerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (playerIdClaim == null || !Guid.TryParse(playerIdClaim, out var playerId))
        {
            return Unauthorized("User ID not found in token.");
        }

        var player = await _context.Players.FindAsync(playerId);
        if (player == null) return NotFound("Player not found.");

        var map = await _context.Maps.FindAsync(request.MapId);
        if (map == null) return NotFound("Map not found.");

        var maxPlayers = Math.Clamp(request.MaxPlayers, 2, 4);
        var lives = Math.Clamp(request.Lives, 1, 10);

        var newRoom = new GameSession
        {
            Id = Guid.NewGuid(),
            MapId = request.MapId,
            MaxPlayers = maxPlayers,
            MinPlayers = 2,
            Lives = lives,
            Status = GameSessionStatus.Waiting
        };

        newRoom.Players.Add(player);
        _context.GameSessions.Add(newRoom);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetRoom), new { id = newRoom.Id }, new RoomResponse(
            newRoom.Id, 
            newRoom.Status, 
            newRoom.MapId,
            map.Name,
            newRoom.MaxPlayers,
            newRoom.MinPlayers,
            newRoom.Lives,
            1,
            false // Can't start with 1 player
        ));
    }

    [HttpPut("{id}/join")]
    public async Task<IActionResult> JoinRoom(Guid id)
    {
        var playerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (playerIdClaim == null || !Guid.TryParse(playerIdClaim, out var playerId))
        {
            return Unauthorized();
        }

        var room = await _context.GameSessions
            .Include(r => r.Players)
            .Include(r => r.Map)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (room == null) return NotFound("Room not found.");
        if (room.Status != GameSessionStatus.Waiting) return BadRequest("Room is no longer waiting for players.");
        if (room.Players.Count >= room.MaxPlayers) return BadRequest("Room is full.");

        if (room.Players.Any(p => p.Id == playerId))
        {
            return Ok(new { Message = "Already in the room." });
        }

        var player = await _context.Players.FindAsync(playerId);
        if (player == null) return NotFound("Player not found.");

        room.Players.Add(player);
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Successfully joined the room." });
    }

    [HttpPut("{id}/leave")]
    public async Task<IActionResult> LeaveRoom(Guid id)
    {
        var playerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (playerIdClaim == null || !Guid.TryParse(playerIdClaim, out var playerId))
        {
            return Unauthorized();
        }

        var room = await _context.GameSessions
            .Include(r => r.Players)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (room == null) return NotFound("Room not found.");
        if (room.Status != GameSessionStatus.Waiting) return BadRequest("Cannot leave a game in progress.");

        var player = room.Players.FirstOrDefault(p => p.Id == playerId);
        if (player == null) return Ok(new { Message = "Not in the room." });

        room.Players.Remove(player);

        // If no players left, delete the room
        if (room.Players.Count == 0)
        {
            _context.GameSessions.Remove(room);
        }

        await _context.SaveChangesAsync();

        return Ok(new { Message = "Successfully left the room." });
    }

    [HttpPut("{id}/start")]
    public async Task<IActionResult> StartRoom(Guid id)
    {
        var playerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (playerIdClaim == null || !Guid.TryParse(playerIdClaim, out var playerId))
        {
            return Unauthorized();
        }

        var room = await _context.GameSessions
            .Include(r => r.Players)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (room == null) return NotFound("Room not found.");
        if (room.Status != GameSessionStatus.Waiting) return BadRequest("Room has already started.");
        
        // Check if requester is the first player (host)
        var firstPlayer = room.Players.FirstOrDefault();
        if (firstPlayer == null || firstPlayer.Id != playerId)
        {
            return Forbid("Only the host can start the game.");
        }

        if (room.Players.Count < room.MinPlayers)
        {
            return BadRequest($"Need at least {room.MinPlayers} players to start.");
        }

        room.Status = GameSessionStatus.InProgress;
        room.StartedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Game started.", RoomId = room.Id });
    }
}
