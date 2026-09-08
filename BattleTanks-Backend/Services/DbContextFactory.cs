using BattleTanks_Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace BattleTanks_Backend.Services;

public interface IDbContextFactory
{
    BattleTanksDbContext CreatePrimaryContext();
    BattleTanksDbContext CreateReplicaContext();
}

public class DbContextFactory : IDbContextFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DbContextFactory> _logger;

    public DbContextFactory(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<DbContextFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public BattleTanksDbContext CreatePrimaryContext()
    {
        var connectionString = _configuration.GetConnectionString("PrimaryConnection");
        var options = new DbContextOptionsBuilder<BattleTanksDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        
        _logger.LogDebug("Created PRIMARY context for write operation");
        return new BattleTanksDbContext(options);
    }

    public BattleTanksDbContext CreateReplicaContext()
    {
        var replicaConnection = _configuration.GetConnectionString("ReplicaConnection");
        var primaryConnection = _configuration.GetConnectionString("PrimaryConnection");
        
        var connectionString = !string.IsNullOrEmpty(replicaConnection) 
            ? replicaConnection 
            : primaryConnection;
        
        var options = new DbContextOptionsBuilder<BattleTanksDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        
        _logger.LogDebug("Created REPLICA context for read operation");
        return new BattleTanksDbContext(options);
    }
}
