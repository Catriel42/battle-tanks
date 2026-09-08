using BattleTanks_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BattleTanks_Backend.Controllers;

[ApiController]
[Route("api/leaderboard")]
public class LeaderboardController : ControllerBase
{
    private readonly LeaderboardService _leaderboardService;
    private readonly RedisCacheService _cacheService;

    public LeaderboardController(LeaderboardService leaderboardService, RedisCacheService cacheService)
    {
        _leaderboardService = leaderboardService;
        _cacheService = cacheService;
    }

    [HttpGet]
    public async Task<IActionResult> GetLeaderboard([FromQuery] int top = 10)
    {
        var topCount = Math.Clamp(top, 1, 100);
        var summary = await _leaderboardService.GetLeaderboardSummaryAsync(topCount);
        
        return Ok(summary);
    }

    [HttpGet("kills")]
    public async Task<IActionResult> GetTopKills([FromQuery] int top = 10)
    {
        var topCount = Math.Clamp(top, 1, 100);
        var leaderboard = await _leaderboardService.GetTopKillsAsync(topCount);
        
        return Ok(new { Type = "kills", Entries = leaderboard });
    }

    [HttpGet("wins")]
    public async Task<IActionResult> GetTopWins([FromQuery] int top = 10)
    {
        var topCount = Math.Clamp(top, 1, 100);
        var leaderboard = await _leaderboardService.GetTopWinsAsync(topCount);
        
        return Ok(new { Type = "wins", Entries = leaderboard });
    }

    [HttpGet("games")]
    public async Task<IActionResult> GetTopGamesPlayed([FromQuery] int top = 10)
    {
        var topCount = Math.Clamp(top, 1, 100);
        var leaderboard = await _leaderboardService.GetTopGamesPlayedAsync(topCount);
        
        return Ok(new { Type = "games", Entries = leaderboard });
    }

    [HttpPost("sync")]
    [Authorize]
    public async Task<IActionResult> SyncFromDatabase()
    {
        await _leaderboardService.SyncLeaderboardFromDatabaseAsync();
        return Ok(new { Message = "Leaderboard synced from database" });
    }

    [HttpGet("online")]
    public async Task<IActionResult> GetOnlinePlayers()
    {
        var count = await _cacheService.GetConnectedPlayersCountAsync();
        var players = await _cacheService.GetConnectedPlayersAsync();
        
        return Ok(new 
        { 
            OnlineCount = count, 
            Players = players.Select(p => new { p.PlayerId, p.Username }) 
        });
    }
}
