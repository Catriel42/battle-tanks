using StackExchange.Redis;
using System.Text.Json;

namespace BattleTanks_Backend.Services;

public class EventHistoryService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<EventHistoryService> _logger;
    private const int HistoryTTLSeconds = 3600; // 1 hour
    
    public EventHistoryService(IConnectionMultiplexer redis, ILogger<EventHistoryService> logger)
    {
        _redis = redis;
        _logger = logger;
    }
    
    public async Task StoreEventAsync(string roomId, string eventType, object payload)
    {
        try
        {
            var db = _redis.GetDatabase();
            var historyKey = $"event_history:{roomId}";
            
            var eventData = new
            {
                type = eventType,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                data = payload
            };
            
            var json = JsonSerializer.Serialize(eventData);
            
            await db.ListLeftPushAsync(historyKey, json);
            
            await db.KeyExpireAsync(historyKey, TimeSpan.FromSeconds(HistoryTTLSeconds));
            
            _logger.LogDebug("Stored event in history for room {RoomId}: {EventType}", roomId, eventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store event in Redis for room {RoomId}", roomId);
        }
    }
    
    public async Task<List<string>> GetHistoryAsync(string roomId, int count = 50)
    {
        try
        {
            var db = _redis.GetDatabase();
            var historyKey = $"event_history:{roomId}";
            
            var events = await db.ListRangeAsync(historyKey, 0, count - 1);
            
            return events
                .Where(e => !e.IsNull)
                .Select(e => e.ToString())
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve event history for room {RoomId}", roomId);
            return [];
        }
    }
    
    public async Task ClearHistoryAsync(string roomId)
    {
        try
        {
            var db = _redis.GetDatabase();
            var historyKey = $"event_history:{roomId}";
            await db.KeyDeleteAsync(historyKey);
            
            _logger.LogInformation("Cleared event history for room {RoomId}", roomId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear event history for room {RoomId}", roomId);
        }
    }
}
