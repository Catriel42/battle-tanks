using BattleTanks_Backend.Data;
using BattleTanks_Backend.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace BattleTanks_Backend.Services;

public class QueryBenchmarkService
{
    private readonly BattleTanksDbContext _context;
    private readonly ILogger<QueryBenchmarkService> _logger;

    public QueryBenchmarkService(BattleTanksDbContext context, ILogger<QueryBenchmarkService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<BenchmarkResult> BenchmarkGetRoomsWithTracking()
    {
        var sw = Stopwatch.StartNew();
        
        var rooms = await _context.GameSessions
            .Include(r => r.Players)
            .Include(r => r.Map)
            .Where(r => r.Status == GameSessionStatus.Waiting)
            .ToListAsync();
        
        sw.Stop();
        
        return new BenchmarkResult("GetRooms_WithTracking", sw.ElapsedMilliseconds, rooms.Count);
    }

    public async Task<BenchmarkResult> BenchmarkGetRoomsNoTracking()
    {
        var sw = Stopwatch.StartNew();
        
        var rooms = await _context.GameSessions
            .AsNoTracking()
            .Include(r => r.Players)
            .Include(r => r.Map)
            .Where(r => r.Status == GameSessionStatus.Waiting)
            .ToListAsync();
        
        sw.Stop();
        
        return new BenchmarkResult("GetRooms_NoTracking", sw.ElapsedMilliseconds, rooms.Count);
    }

    public async Task<BenchmarkResult> BenchmarkGetPlayerStatsWithTracking(Guid playerId)
    {
        var sw = Stopwatch.StartNew();
        
        var stats = await _context.PlayerGameStats
            .Include(s => s.GameSession)
            .Where(s => s.PlayerId == playerId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(50)
            .ToListAsync();
        
        sw.Stop();
        
        return new BenchmarkResult("GetPlayerStats_WithTracking", sw.ElapsedMilliseconds, stats.Count);
    }

    public async Task<BenchmarkResult> BenchmarkGetPlayerStatsNoTracking(Guid playerId)
    {
        var sw = Stopwatch.StartNew();
        
        var stats = await _context.PlayerGameStats
            .AsNoTracking()
            .Include(s => s.GameSession)
            .Where(s => s.PlayerId == playerId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(50)
            .ToListAsync();
        
        sw.Stop();
        
        return new BenchmarkResult("GetPlayerStats_NoTracking", sw.ElapsedMilliseconds, stats.Count);
    }

    public async Task<BenchmarkResult> BenchmarkLeaderboardWithIndex()
    {
        var sw = Stopwatch.StartNew();
        
        var leaderboard = await _context.PlayerGameStats
            .AsNoTracking()
            .GroupBy(s => s.PlayerId)
            .Select(g => new
            {
                PlayerId = g.Key,
                TotalKills = g.Sum(s => s.Kills),
                TotalGames = g.Count()
            })
            .OrderByDescending(x => x.TotalKills)
            .Take(100)
            .ToListAsync();
        
        sw.Stop();
        
        return new BenchmarkResult("Leaderboard_WithIndex", sw.ElapsedMilliseconds, leaderboard.Count);
    }

    public async Task<List<BenchmarkResult>> RunAllBenchmarks(Guid? playerId = null)
    {
        var results = new List<BenchmarkResult>();
        
        // Run each benchmark multiple times for averaging
        const int iterations = 5;
        
        for (int i = 0; i < iterations; i++)
        {
            results.Add(await BenchmarkGetRoomsWithTracking());
            results.Add(await BenchmarkGetRoomsNoTracking());
            
            if (playerId.HasValue)
            {
                results.Add(await BenchmarkGetPlayerStatsWithTracking(playerId.Value));
                results.Add(await BenchmarkGetPlayerStatsNoTracking(playerId.Value));
            }
            
            results.Add(await BenchmarkLeaderboardWithIndex());
        }
        
        // Group and average results
        var averaged = results
            .GroupBy(r => r.QueryName)
            .Select(g => new BenchmarkResult(
                g.Key,
                (long)g.Average(r => r.ElapsedMs),
                g.First().ResultCount
            ))
            .ToList();
        
        foreach (var result in averaged)
        {
            _logger.LogInformation("Benchmark {Query}: {Elapsed}ms ({Count} results)",
                result.QueryName, result.ElapsedMs, result.ResultCount);
        }
        
        return averaged;
    }
}

public record BenchmarkResult(string QueryName, long ElapsedMs, int ResultCount);
