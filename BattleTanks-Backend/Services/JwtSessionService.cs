using StackExchange.Redis;

namespace BattleTanks_Backend.Services;

public class JwtSessionService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<JwtSessionService> _logger;
    
    private const string SessionKeyPrefix = "session:";
    private const string BlacklistKeyPrefix = "blacklist:";
    private const string ActiveSessionsKey = "active_sessions";
    private static readonly TimeSpan DefaultSessionTtl = TimeSpan.FromHours(2);

    public JwtSessionService(IConnectionMultiplexer redis, ILogger<JwtSessionService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task StoreSessionAsync(Guid playerId, string jti, string token, TimeSpan? ttl = null)
    {
        try
        {
            var db = _redis.GetDatabase();
            var sessionKey = $"{SessionKeyPrefix}{playerId}";
            var expiry = ttl ?? DefaultSessionTtl;
            
            var sessionData = new HashEntry[]
            {
                new("jti", jti),
                new("token", token),
                new("created_at", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()),
                new("expires_at", DateTimeOffset.UtcNow.Add(expiry).ToUnixTimeMilliseconds().ToString())
            };
            
            await db.HashSetAsync(sessionKey, sessionData);
            await db.KeyExpireAsync(sessionKey, expiry);
            
            await db.SetAddAsync(ActiveSessionsKey, playerId.ToString());
            
            _logger.LogInformation("Session stored for player {PlayerId}, expires in {Ttl}", playerId, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store session for player {PlayerId}", playerId);
        }
    }

    public async Task<bool> ValidateSessionAsync(Guid playerId, string jti)
    {
        try
        {
            var db = _redis.GetDatabase();
            
            if (await IsTokenBlacklistedAsync(jti))
            {
                _logger.LogWarning("Token {Jti} is blacklisted for player {PlayerId}", jti, playerId);
                return false;
            }
            
            var sessionKey = $"{SessionKeyPrefix}{playerId}";
            var storedJti = await db.HashGetAsync(sessionKey, "jti");
            
            if (storedJti.IsNull)
            {
                _logger.LogDebug("No session found for player {PlayerId}", playerId);
                return false;
            }
            
            var isValid = storedJti.ToString() == jti;
            
            if (!isValid)
            {
                _logger.LogWarning("Session JTI mismatch for player {PlayerId}", playerId);
            }
            
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate session for player {PlayerId}", playerId);
            return false;
        }
    }

    public async Task InvalidateSessionAsync(Guid playerId, string jti, TimeSpan? blacklistTtl = null)
    {
        try
        {
            var db = _redis.GetDatabase();
            var sessionKey = $"{SessionKeyPrefix}{playerId}";
            
            await db.KeyDeleteAsync(sessionKey);
            
            await db.SetRemoveAsync(ActiveSessionsKey, playerId.ToString());
            
            await BlacklistTokenAsync(jti, blacklistTtl ?? DefaultSessionTtl);
            
            _logger.LogInformation("Session invalidated for player {PlayerId}, token {Jti} blacklisted", playerId, jti);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate session for player {PlayerId}", playerId);
        }
    }

    public async Task BlacklistTokenAsync(string jti, TimeSpan? ttl = null)
    {
        try
        {
            var db = _redis.GetDatabase();
            var blacklistKey = $"{BlacklistKeyPrefix}{jti}";
            var expiry = ttl ?? DefaultSessionTtl;
            
            await db.StringSetAsync(blacklistKey, "revoked", expiry);
            
            _logger.LogDebug("Token {Jti} added to blacklist, expires in {Ttl}", jti, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to blacklist token {Jti}", jti);
        }
    }

    public async Task<bool> IsTokenBlacklistedAsync(string jti)
    {
        try
        {
            var db = _redis.GetDatabase();
            var blacklistKey = $"{BlacklistKeyPrefix}{jti}";
            
            return await db.KeyExistsAsync(blacklistKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check blacklist for token {Jti}", jti);
            return false;
        }
    }

    public async Task<SessionInfo?> GetSessionInfoAsync(Guid playerId)
    {
        try
        {
            var db = _redis.GetDatabase();
            var sessionKey = $"{SessionKeyPrefix}{playerId}";
            
            var sessionData = await db.HashGetAllAsync(sessionKey);
            
            if (sessionData.Length == 0)
            {
                return null;
            }
            
            var dict = sessionData.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
            
            return new SessionInfo(
                playerId,
                dict.GetValueOrDefault("jti") ?? "",
                long.TryParse(dict.GetValueOrDefault("created_at"), out var created) ? created : 0,
                long.TryParse(dict.GetValueOrDefault("expires_at"), out var expires) ? expires : 0
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get session info for player {PlayerId}", playerId);
            return null;
        }
    }

    public async Task<int> GetActiveSessionCountAsync()
    {
        try
        {
            var db = _redis.GetDatabase();
            return (int)await db.SetLengthAsync(ActiveSessionsKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active session count");
            return 0;
        }
    }

    public async Task<bool> HasActiveSessionAsync(Guid playerId)
    {
        try
        {
            var db = _redis.GetDatabase();
            var sessionKey = $"{SessionKeyPrefix}{playerId}";
            return await db.KeyExistsAsync(sessionKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check active session for player {PlayerId}", playerId);
            return false;
        }
    }
}

public record SessionInfo(Guid PlayerId, string Jti, long CreatedAtMs, long ExpiresAtMs);
