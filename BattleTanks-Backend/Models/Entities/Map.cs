namespace BattleTanks_Backend.Models.Entities;

public class Map
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string TileData { get; set; } = string.Empty; // JSON array of tile grid
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<GameSession> GameSessions { get; set; } = new List<GameSession>();
}
