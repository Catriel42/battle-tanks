using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using System.Text.Json;

namespace BattleTanks_Backend.Services;

public class MqttPublisherService : IHostedService
{
    private readonly IConfiguration _config;
    private readonly EventHistoryService _historyService;
    private readonly ILogger<MqttPublisherService> _logger;
    private IMqttClient? _mqttClient;
    private bool _isConnected;
    
    public MqttPublisherService(IConfiguration config, EventHistoryService historyService, ILogger<MqttPublisherService> logger)
    {
        _config = config;
        _historyService = historyService;
        _logger = logger;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var mqttConfig = _config.GetSection("Mqtt");
            var host = mqttConfig["Host"] ?? "localhost";
            var port = int.Parse(mqttConfig["Port"] ?? "1883");
            
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(host, port)
                .WithCleanSession()
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(60))
                .Build();
            
            _mqttClient = new MqttFactory().CreateMqttClient();
            
            _mqttClient.ConnectedAsync += async e =>
            {
                _isConnected = true;
                _logger.LogInformation("MQTT client connected to {Host}:{Port}", host, port);
                await Task.CompletedTask;
            };
            
            _mqttClient.DisconnectedAsync += async e =>
            {
                _isConnected = false;
                _logger.LogWarning("MQTT client disconnected. Reconnecting...");
                
                await Task.Delay(5000, cancellationToken);
                if (!cancellationToken.IsCancellationRequested && _mqttClient != null)
                {
                    try
                    {
                        await _mqttClient.ConnectAsync(options, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to reconnect MQTT client");
                    }
                }
            };
            
            if (_mqttClient != null)
            {
                await _mqttClient.ConnectAsync(options, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start MQTT client");
            throw;
        }
    }
    
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_mqttClient != null)
        {
            await _mqttClient.DisconnectAsync(cancellationToken: cancellationToken);
            _mqttClient.Dispose();
        }
    }
    
    public async Task PublishAsync(string topic, string payload, byte qos = 1)
    {
        if (_mqttClient == null || !_isConnected)
        {
            _logger.LogWarning("MQTT client not connected. Cannot publish to {Topic}", topic);
            return;
        }
        
        try
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)qos)
                .Build();
            
            await _mqttClient.PublishAsync(message);
            _logger.LogDebug("Published message to {Topic}", topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish MQTT message to {Topic}", topic);
        }
    }
    
    public async Task PublishJsonAsync(string topic, object payload, byte qos = 1)
    {
        var json = JsonSerializer.Serialize(payload);
        await PublishAsync(topic, json, qos);
    }
    
    public async Task PublishPowerUpSpawnedAsync(string roomId, Guid powerUpId, string type, double x, double y, long timestamp)
    {
        var topic = $"game/{roomId}/powerup/spawned";
        var payload = new
        {
            id = powerUpId,
            type,
            x,
            y,
            timestamp,
            sent_at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        
        await PublishJsonAsync(topic, payload);
        await _historyService.StoreEventAsync(roomId, "powerup_spawned", payload);
    }
    
    public async Task PublishPowerUpCollectedAsync(string roomId, Guid powerUpId, string playerId, string username, int newLives, long timestamp)
    {
        var topic = $"game/{roomId}/powerup/collected";
        var payload = new
        {
            id = powerUpId,
            player_id = playerId,
            username,
            new_lives = newLives,
            timestamp,
            sent_at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        
        await PublishJsonAsync(topic, payload);
        await _historyService.StoreEventAsync(roomId, "powerup_collected", payload);
    }
    
    public async Task PublishCollisionAsync(string roomId, string attackerId, string victimId, int damage, long timestamp)
    {
        var topic = $"game/{roomId}/collision";
        var payload = new
        {
            attacker_id = attackerId,
            victim_id = victimId,
            damage,
            timestamp,
            sent_at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        
        await PublishJsonAsync(topic, payload);
        await _historyService.StoreEventAsync(roomId, "collision", payload);
    }
    
    public async Task PublishGameOverAsync(string roomId, string winnerId, string winnerUsername, long timestamp)
    {
        var topic = $"game/{roomId}/gameover";
        var payload = new
        {
            winner_id = winnerId,
            winner_username = winnerUsername,
            timestamp,
            sent_at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        
        await PublishJsonAsync(topic, payload);
        await _historyService.StoreEventAsync(roomId, "gameover", payload);
    }
    
    public bool IsConnected => _isConnected;
}
