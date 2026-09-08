using BattleTanks_Backend.Data;
using BattleTanks_Backend.Models.Entities;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;

namespace BattleTanks_Backend.Services;

public class BulkOperationsService
{
    private readonly BattleTanksDbContext _context;
    private readonly ILogger<BulkOperationsService> _logger;

    public BulkOperationsService(BattleTanksDbContext context, ILogger<BulkOperationsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task BulkInsertPlayerStatsAsync(IList<PlayerGameStats> stats)
    {
        if (stats.Count == 0) return;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _context.BulkInsertAsync(stats);
        sw.Stop();

        _logger.LogInformation("BulkInsert: {Count} PlayerGameStats in {Elapsed}ms", 
            stats.Count, sw.ElapsedMilliseconds);
    }

    public async Task BulkUpdatePlayerStatsAsync(IList<PlayerGameStats> stats)
    {
        if (stats.Count == 0) return;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _context.BulkUpdateAsync(stats);
        sw.Stop();

        _logger.LogInformation("BulkUpdate: {Count} PlayerGameStats in {Elapsed}ms", 
            stats.Count, sw.ElapsedMilliseconds);
    }

    public async Task BulkDeleteOldSessionsAsync(DateTime cutoffDate)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        var oldSessions = await _context.GameSessions
            .Where(s => s.Status == GameSessionStatus.Finished && s.FinishedAt < cutoffDate)
            .ToListAsync();

        if (oldSessions.Count > 0)
        {
            await _context.BulkDeleteAsync(oldSessions);
        }
        
        sw.Stop();

        _logger.LogInformation("BulkDelete: {Count} old GameSessions in {Elapsed}ms", 
            oldSessions.Count, sw.ElapsedMilliseconds);
    }

    public async Task BulkUpdatePlayerLifetimeStatsAsync(Guid gameSessionId)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var gameStats = await _context.PlayerGameStats
            .AsNoTracking()
            .Where(s => s.GameSessionId == gameSessionId)
            .ToListAsync();

        if (gameStats.Count == 0) return;

        var playerIds = gameStats.Select(s => s.PlayerId).Distinct().ToList();
        var players = await _context.Players
            .Where(p => playerIds.Contains(p.Id))
            .ToListAsync();

        foreach (var player in players)
        {
            var stat = gameStats.First(s => s.PlayerId == player.Id);
            player.GamesPlayed++;
            player.TotalKills += stat.Kills;
            player.TotalDeaths += stat.Deaths;
            if (stat.FinalPosition == 1) player.Wins++;
        }

        await _context.BulkUpdateAsync(players);
        sw.Stop();

        _logger.LogInformation("BulkUpdate: {Count} Player lifetime stats in {Elapsed}ms", 
            players.Count, sw.ElapsedMilliseconds);
    }
}
