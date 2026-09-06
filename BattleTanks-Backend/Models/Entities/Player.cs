namespace BattleTanks_Backend.Models.Entities;

public class Player
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    
    // Lifetime stats
    public int GamesPlayed { get; set; }
    public int Wins { get; set; }
    public int TotalKills { get; set; }
    public int TotalDeaths { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<GameSession> GameSessions { get; set; } = new List<GameSession>();
    public ICollection<GameSession> WonSessions { get; set; } = new List<GameSession>();
    public ICollection<PlayerGameStats> PlayerStats { get; set; } = new List<PlayerGameStats>();
}
