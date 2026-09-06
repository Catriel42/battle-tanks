namespace BattleTanks_Backend.Models.GameState;

public enum Direction
{
    Up,
    Down,
    Left,
    Right
}

public static class DirectionExtensions
{
    public static (double dx, double dy) ToVector(this Direction direction)
    {
        return direction switch
        {
            Direction.Up => (0, -1),
            Direction.Down => (0, 1),
            Direction.Left => (-1, 0),
            Direction.Right => (1, 0),
            _ => (0, 0)
        };
    }

    public static double ToRadians(this Direction direction)
    {
        return direction switch
        {
            Direction.Up => 0,
            Direction.Down => Math.PI,
            Direction.Left => -Math.PI / 2,
            Direction.Right => Math.PI / 2,
            _ => 0
        };
    }
}
