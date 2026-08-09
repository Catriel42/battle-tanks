namespace BattleTanks_Backend.Models.Entities;

public class Score
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public Guid GameSessionId { get; set; }
    public int Points { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Player Player { get; set; } = null!;
    public GameSession GameSession { get; set; } = null!;
}
