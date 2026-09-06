namespace BattleTanks_Backend.Models.Entities;

public class PlayerGameStats
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public Guid GameSessionId { get; set; }

    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int ShotsFired { get; set; }
    public int ShotsHit { get; set; }
    public double Accuracy { get; set; } // Percentage 0-100
    public int BlocksDestroyed { get; set; }
    public int DamageDealt { get; set; }
    public int DamageTaken { get; set; }
    public int FinalPosition { get; set; } // 1st, 2nd, 3rd, 4th
    public long SurvivalTimeMs { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Player Player { get; set; } = null!;
    public GameSession GameSession { get; set; } = null!;
}
