using StackExchange.Redis;

namespace BattleTanks_Backend.Services;

public class RedisCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;
    
    private const string ConnectedPlayersKey = "connected_players";
    private const string PlayerConnectionKey = "player_connection:";
    private const string RoomPlayersKey = "room_players:";

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task AddConnectedPlayerAsync(Guid playerId, string connectionId, string username)
    {
        try
        {
            var db = _redis.GetDatabase();
            var playerData = $"{playerId}:{username}:{connectionId}";
            
            await db.SetAddAsync(ConnectedPlayersKey, playerData);
            await db.StringSetAsync($"{PlayerConnectionKey}{playerId}", connectionId, TimeSpan.FromHours(2));
            
            _logger.LogInformation("Player {Username} ({PlayerId}) added to connected players cache", username, playerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add player {PlayerId} to Redis cache", playerId);
        }
    }

    public async Task RemoveConnectedPlayerAsync(Guid playerId, string connectionId, string username)
    {
        try
        {
            var db = _redis.GetDatabase();
            var playerData = $"{playerId}:{username}:{connectionId}";
            
            await db.SetRemoveAsync(ConnectedPlayersKey, playerData);
            await db.KeyDeleteAsync($"{PlayerConnectionKey}{playerId}");
            
            _logger.LogInformation("Player {Username} ({PlayerId}) removed from connected players cache", username, playerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove player {PlayerId} from Redis cache", playerId);
        }
    }

    public async Task<int> GetConnectedPlayersCountAsync()
    {
        try
        {
            var db = _redis.GetDatabase();
            return (int)await db.SetLengthAsync(ConnectedPlayersKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get connected players count from Redis");
            return 0;
        }
    }

    public async Task<List<ConnectedPlayerInfo>> GetConnectedPlayersAsync()
    {
        try
        {
            var db = _redis.GetDatabase();
            var members = await db.SetMembersAsync(ConnectedPlayersKey);
            
            var players = new List<ConnectedPlayerInfo>();
            foreach (var member in members)
            {
                var parts = member.ToString().Split(':');
                if (parts.Length >= 3 && Guid.TryParse(parts[0], out var playerId))
                {
                    players.Add(new ConnectedPlayerInfo(playerId, parts[1], parts[2]));
                }
            }
            
            return players;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get connected players from Redis");
            return new List<ConnectedPlayerInfo>();
        }
    }

    public async Task<bool> IsPlayerConnectedAsync(Guid playerId)
    {
        try
        {
            var db = _redis.GetDatabase();
            return await db.KeyExistsAsync($"{PlayerConnectionKey}{playerId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check player connection status for {PlayerId}", playerId);
            return false;
        }
    }

    public async Task AddPlayerToRoomAsync(string roomId, Guid playerId, string username)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.SetAddAsync($"{RoomPlayersKey}{roomId}", $"{playerId}:{username}");
            
            _logger.LogDebug("Player {Username} added to room {RoomId} cache", username, roomId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add player to room cache");
        }
    }

    public async Task RemovePlayerFromRoomAsync(string roomId, Guid playerId, string username)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.SetRemoveAsync($"{RoomPlayersKey}{roomId}", $"{playerId}:{username}");
            
            _logger.LogDebug("Player {Username} removed from room {RoomId} cache", username, roomId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove player from room cache");
        }
    }

    public async Task<int> GetRoomPlayerCountAsync(string roomId)
    {
        try
        {
            var db = _redis.GetDatabase();
            return (int)await db.SetLengthAsync($"{RoomPlayersKey}{roomId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get room player count");
            return 0;
        }
    }

    public async Task ClearRoomPlayersAsync(string roomId)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync($"{RoomPlayersKey}{roomId}");
            
            _logger.LogDebug("Room {RoomId} players cache cleared", roomId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear room players cache");
        }
    }
}

public record ConnectedPlayerInfo(Guid PlayerId, string Username, string ConnectionId);
