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
            .Include(r => r.Players)
            .Where(r => r.Status == GameSessionStatus.Waiting)
            .Select(r => new RoomResponse(r.Id, r.Status, r.MapName, r.MaxPlayers, r.Players.Count))
            .ToListAsync();

        return Ok(rooms);
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

        var newRoom = new GameSession
        {
            Id = Guid.NewGuid(),
            MapName = request.MapName,
            MaxPlayers = request.MaxPlayers > 0 ? request.MaxPlayers : 4,
            Status = GameSessionStatus.Waiting
        };

        newRoom.Players.Add(player);
        _context.GameSessions.Add(newRoom);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetRooms), new RoomResponse(newRoom.Id, newRoom.Status, newRoom.MapName, newRoom.MaxPlayers, 1));
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
        
        // Optional: Auto-start if full
        // if (room.Players.Count == room.MaxPlayers) room.Status = GameSessionStatus.InProgress;

        await _context.SaveChangesAsync();

        return Ok(new { Message = "Successfully joined the room." });
    }
}
