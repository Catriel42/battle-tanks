using BattleTanks_Backend.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BattleTanks_Backend.Data;

public class BattleTanksDbContext : DbContext
{
    public BattleTanksDbContext(DbContextOptions<BattleTanksDbContext> options)
        : base(options)
    {
    }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<Map> Maps => Set<Map>();
    public DbSet<PlayerGameStats> PlayerGameStats => Set<PlayerGameStats>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Player configuration
        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PasswordHash).IsRequired();
        });

        // Map configuration
        modelBuilder.Entity<Map>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.Property(e => e.TileData).IsRequired();
        });

        // GameSession configuration
        modelBuilder.Entity<GameSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasConversion<string>();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.Status, e.CreatedAt });

            entity.HasOne(e => e.Map)
                .WithMany(m => m.GameSessions)
                .HasForeignKey(e => e.MapId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Winner)
                .WithMany(p => p.WonSessions)
                .HasForeignKey(e => e.WinnerId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            entity.HasMany(e => e.Players)
                .WithMany(p => p.GameSessions);
        });

        // PlayerGameStats configuration
        modelBuilder.Entity<PlayerGameStats>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Player)
                .WithMany(p => p.PlayerStats)
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.GameSession)
                .WithMany(g => g.PlayerStats)
                .HasForeignKey(e => e.GameSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.PlayerId, e.GameSessionId }).IsUnique();            
            entity.HasIndex(e => e.PlayerId);
            entity.HasIndex(e => e.GameSessionId);
            entity.HasIndex(e => e.Kills);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Seed default map
        var defaultMapId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        modelBuilder.Entity<Map>().HasData(new Map
        {
            Id = defaultMapId,
            Name = "Classic",
            Width = 30,
            Height = 20,
            TileData = """
            [
              [1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1],
              [1,0,0,0,0,0,2,2,0,0,1,1,0,0,0,0,0,0,1,1,0,0,2,2,0,0,0,0,0,1],
              [1,0,1,1,2,0,0,0,0,0,1,1,0,2,2,2,2,0,1,1,0,0,0,0,0,2,1,1,0,1],
              [1,0,1,1,2,0,1,1,1,0,0,0,0,2,1,1,2,0,0,0,0,1,1,1,0,2,1,1,0,1],
              [1,0,2,2,0,0,1,1,1,0,2,2,0,2,1,1,2,0,2,2,0,1,1,1,0,0,2,2,0,1],
              [1,0,0,0,0,0,0,2,0,0,2,2,0,0,0,0,0,0,2,2,0,0,2,0,0,0,0,0,0,1],
              [1,1,1,0,2,2,0,0,0,0,0,0,0,1,1,1,1,0,0,0,0,0,0,0,2,2,0,1,1,1],
              [1,1,1,0,1,1,0,1,1,1,1,0,0,1,1,1,1,0,0,1,1,1,1,0,1,1,0,1,1,1],
              [1,0,0,0,1,1,0,1,1,1,1,0,0,0,0,0,0,0,0,1,1,1,1,0,1,1,0,0,0,1],
              [1,0,2,2,0,0,0,0,2,2,0,0,1,1,0,0,1,1,0,0,2,2,0,0,0,0,2,2,0,1],
              [1,0,2,2,0,0,0,0,2,2,0,0,1,1,0,0,1,1,0,0,2,2,0,0,0,0,2,2,0,1],
              [1,0,0,0,1,1,0,1,1,1,1,0,0,0,0,0,0,0,0,1,1,1,1,0,1,1,0,0,0,1],
              [1,1,1,0,1,1,0,1,1,1,1,0,0,1,1,1,1,0,0,1,1,1,1,0,1,1,0,1,1,1],
              [1,1,1,0,2,2,0,0,0,0,0,0,0,1,1,1,1,0,0,0,0,0,0,0,2,2,0,1,1,1],
              [1,0,0,0,0,0,0,2,0,0,2,2,0,0,0,0,0,0,2,2,0,0,2,0,0,0,0,0,0,1],
              [1,0,2,2,0,0,1,1,1,0,2,2,0,2,1,1,2,0,2,2,0,1,1,1,0,0,2,2,0,1],
              [1,0,1,1,2,0,1,1,1,0,0,0,0,2,1,1,2,0,0,0,0,1,1,1,0,2,1,1,0,1],
              [1,0,1,1,2,0,0,0,0,0,1,1,0,2,2,2,2,0,1,1,0,0,0,0,0,2,1,1,0,1],
              [1,0,0,0,0,0,2,2,0,0,1,1,0,0,0,0,0,0,1,1,0,0,2,2,0,0,0,0,0,1],
              [1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1]
            ]
            """,
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
