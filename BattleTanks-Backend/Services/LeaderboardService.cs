using BattleTanks_Backend.Data;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace BattleTanks_Backend.Services;

public class LeaderboardService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly BattleTanksDbContext _dbContext;
    private readonly ILogger<LeaderboardService> _logger;
    
    private const string LeaderboardKillsKey = "leaderboard:kills";
    private const string LeaderboardWinsKey = "leaderboard:wins";
    private const string LeaderboardGamesKey = "leaderboard:games";
    private const int DefaultTopCount = 10;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public LeaderboardService(
        IConnectionMultiplexer redis, 
        BattleTanksDbContext dbContext,
        ILogger<LeaderboardService> logger)
    {
        _redis = redis;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task UpdatePlayerScoreAsync(Guid playerId, string username, int kills, int wins, int gamesPlayed)
    {
        try
        {
            var db = _redis.GetDatabase();
            var member = $"{playerId}:{username}";
            
            await db.SortedSetAddAsync(LeaderboardKillsKey, member, kills);
            await db.SortedSetAddAsync(LeaderboardWinsKey, member, wins);
            await db.SortedSetAddAsync(LeaderboardGamesKey, member, gamesPlayed);
            
            _logger.LogDebug("Updated leaderboard scores for player {Username}: kills={Kills}, wins={Wins}, games={Games}", 
                username, kills, wins, gamesPlayed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update leaderboard scores for player {PlayerId}", playerId);
        }
    }

    public async Task IncrementKillsAsync(Guid playerId, string username, int killsToAdd = 1)
    {
        try
        {
            var db = _redis.GetDatabase();
            var member = $"{playerId}:{username}";
            
            await db.SortedSetIncrementAsync(LeaderboardKillsKey, member, killsToAdd);
            
            _logger.LogDebug("Incremented kills for player {Username} by {Kills}", username, killsToAdd);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to increment kills for player {PlayerId}", playerId);
        }
    }

    public async Task<List<LeaderboardEntry>> GetTopKillsAsync(int count = DefaultTopCount)
    {
        return await GetTopFromSortedSetAsync(LeaderboardKillsKey, "kills", count);
    }

    public async Task<List<LeaderboardEntry>> GetTopWinsAsync(int count = DefaultTopCount)
    {
        return await GetTopFromSortedSetAsync(LeaderboardWinsKey, "wins", count);
    }

    public async Task<List<LeaderboardEntry>> GetTopGamesPlayedAsync(int count = DefaultTopCount)
    {
        return await GetTopFromSortedSetAsync(LeaderboardGamesKey, "games", count);
    }

    private async Task<List<LeaderboardEntry>> GetTopFromSortedSetAsync(string key, string scoreType, int count)
    {
        try
        {
            var db = _redis.GetDatabase();
            var entries = await db.SortedSetRangeByRankWithScoresAsync(key, 0, count - 1, Order.Descending);
            
            var leaderboard = new List<LeaderboardEntry>();
            int rank = 1;
            
            foreach (var entry in entries)
            {
                var parts = entry.Element.ToString().Split(':');
                if (parts.Length >= 2 && Guid.TryParse(parts[0], out var playerId))
                {
                    leaderboard.Add(new LeaderboardEntry(
                        rank++,
                        playerId,
                        parts[1],
                        (int)entry.Score,
                        scoreType
                    ));
                }
            }
            
            return leaderboard;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get top {Count} from {Key}", count, key);
            return new List<LeaderboardEntry>();
        }
    }

    public async Task<int?> GetPlayerRankAsync(Guid playerId, string username, string leaderboardType = "kills")
    {
        try
        {
            var db = _redis.GetDatabase();
            var member = $"{playerId}:{username}";
            var key = leaderboardType.ToLower() switch
            {
                "wins" => LeaderboardWinsKey,
                "games" => LeaderboardGamesKey,
                _ => LeaderboardKillsKey
            };
            
            var rank = await db.SortedSetRankAsync(key, member, Order.Descending);
            return rank.HasValue ? (int)rank.Value + 1 : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get rank for player {PlayerId}", playerId);
            return null;
        }
    }

    public async Task SyncLeaderboardFromDatabaseAsync()
    {
        try
        {
            _logger.LogInformation("Starting leaderboard sync from database...");
            
            var players = await _dbContext.Players
                .AsNoTracking()
                .Where(p => p.GamesPlayed > 0)
                .Select(p => new { p.Id, p.Username, p.TotalKills, p.Wins, p.GamesPlayed })
                .ToListAsync();
            
            var db = _redis.GetDatabase();
            
            await db.KeyDeleteAsync(LeaderboardKillsKey);
            await db.KeyDeleteAsync(LeaderboardWinsKey);
            await db.KeyDeleteAsync(LeaderboardGamesKey);
            
            foreach (var player in players)
            {
                var member = $"{player.Id}:{player.Username}";
                await db.SortedSetAddAsync(LeaderboardKillsKey, member, player.TotalKills);
                await db.SortedSetAddAsync(LeaderboardWinsKey, member, player.Wins);
                await db.SortedSetAddAsync(LeaderboardGamesKey, member, player.GamesPlayed);
            }
            
            _logger.LogInformation("Leaderboard sync complete. Synced {Count} players", players.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync leaderboard from database");
        }
    }

    public async Task<LeaderboardSummary> GetLeaderboardSummaryAsync(int topCount = DefaultTopCount)
    {
        var topKills = await GetTopKillsAsync(topCount);
        var topWins = await GetTopWinsAsync(topCount);
        var topGames = await GetTopGamesPlayedAsync(topCount);
        
        return new LeaderboardSummary(topKills, topWins, topGames, DateTime.UtcNow);
    }
}

public record LeaderboardEntry(int Rank, Guid PlayerId, string Username, int Score, string ScoreType);

public record LeaderboardSummary(
    List<LeaderboardEntry> TopKills,
    List<LeaderboardEntry> TopWins,
    List<LeaderboardEntry> TopGamesPlayed,
    DateTime GeneratedAt
);
