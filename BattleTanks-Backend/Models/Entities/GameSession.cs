namespace BattleTanks_Backend.Models.Entities;

public enum GameSessionStatus
{
    Waiting,
    InProgress,
    Finished,
    Cancelled
}

public class GameSession
{
    public Guid Id { get; set; }
    public GameSessionStatus Status { get; set; } = GameSessionStatus.Waiting;
    
    public Guid MapId { get; set; }
    public int MaxPlayers { get; set; } = 4;
    public int MinPlayers { get; set; } = 2;
    public int Lives { get; set; } = 3;
    
    public Guid? WinnerId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public long? DurationMs { get; set; }

    // Navigation properties
    public Map Map { get; set; } = null!;
    public Player? Winner { get; set; }
    public ICollection<Player> Players { get; set; } = new List<Player>();
    public ICollection<PlayerGameStats> PlayerStats { get; set; } = new List<PlayerGameStats>();
}
