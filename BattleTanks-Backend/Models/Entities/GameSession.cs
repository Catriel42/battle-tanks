namespace BattleTanks_Backend.Models.Entities;

public enum GameSessionStatus
{
    Waiting,
    InProgress,
    Finished
}

public class GameSession
{
    public Guid Id { get; set; }
    public GameSessionStatus Status { get; set; } = GameSessionStatus.Waiting;
    public string MapName { get; set; } = "DefaultMap";
    public int MaxPlayers { get; set; } = 4;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }

    public ICollection<Score> Scores { get; set; } = new List<Score>();
}
