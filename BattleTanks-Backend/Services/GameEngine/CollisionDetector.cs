using BattleTanks_Backend.Models.GameState;

namespace BattleTanks_Backend.Services.GameEngine;

public static class CollisionDetector
{
    /// <summary>
    /// Check if two rectangles overlap (AABB collision)
    /// </summary>
    public static bool RectsOverlap(
        double x1, double y1, double w1, double h1,
        double x2, double y2, double w2, double h2)
    {
        return x1 < x2 + w2 &&
               x1 + w1 > x2 &&
               y1 < y2 + h2 &&
               y1 + h1 > y2;
    }
    
    /// <summary>
    /// Check if a point is inside a rectangle
    /// </summary>
    public static bool PointInRect(double px, double py, double rx, double ry, double rw, double rh)
    {
        return px >= rx && px < rx + rw && py >= ry && py < ry + rh;
    }
    
    /// <summary>
    /// Get the tile coordinates for a world position
    /// </summary>
    public static (int row, int col) WorldToTile(double x, double y)
    {
        var col = (int)(x / GameConstants.TileSize);
        var row = (int)(y / GameConstants.TileSize);
        return (row, col);
    }
    
    /// <summary>
    /// Get the world position for tile coordinates (top-left corner)
    /// </summary>
    public static (double x, double y) TileToWorld(int row, int col)
    {
        return (col * GameConstants.TileSize, row * GameConstants.TileSize);
    }
    
    /// <summary>
    /// Get all tiles that a rectangle overlaps with
    /// </summary>
    public static IEnumerable<(int row, int col)> GetOverlappingTiles(double x, double y, double width, double height)
    {
        var (startRow, startCol) = WorldToTile(x, y);
        var (endRow, endCol) = WorldToTile(x + width - 1, y + height - 1);
        
        for (var row = startRow; row <= endRow; row++)
        {
            for (var col = startCol; col <= endCol; col++)
            {
                yield return (row, col);
            }
        }
    }
    
    /// <summary>
    /// Check if a tank at a given position would collide with any blocking tiles
    /// </summary>
    public static bool TankCollidesWithMap(double x, double y, GameRoomState room)
    {
        var tiles = GetOverlappingTiles(x, y, GameConstants.TankSize, GameConstants.TankSize);
        
        foreach (var (row, col) in tiles)
        {
            if (room.IsTileBlocking(row, col))
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Check if a tank at a given position would collide with another tank
    /// </summary>
    public static bool TankCollidesWithTank(double x, double y, string excludeTankId, GameRoomState room)
    {
        foreach (var (connectionId, tank) in room.Tanks)
        {
            if (connectionId == excludeTankId || !tank.IsAlive)
                continue;
            
            if (RectsOverlap(
                x, y, GameConstants.TankSize, GameConstants.TankSize,
                tank.X, tank.Y, GameConstants.TankSize, GameConstants.TankSize))
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Check if a bullet at a given position hits any blocking tile
    /// Returns the tile coordinates if hit, null otherwise
    /// </summary>
    public static (int row, int col)? BulletHitsTile(double x, double y, GameRoomState room)
    {
        // Check center point of bullet for tile collision
        var centerX = x + GameConstants.BulletSize / 2.0;
        var centerY = y + GameConstants.BulletSize / 2.0;
        var (row, col) = WorldToTile(centerX, centerY);
        
        if (room.IsTileBlocking(row, col))
        {
            return (row, col);
        }
        
        return null;
    }
    
    /// <summary>
    /// Check if a bullet at a given position hits any tank
    /// Returns the tank if hit, null otherwise
    /// </summary>
    public static TankState? BulletHitsTank(double x, double y, string shooterId, GameRoomState room)
    {
        foreach (var (connectionId, tank) in room.Tanks)
        {
            // Don't hit the shooter or dead tanks
            if (connectionId == shooterId || !tank.IsAlive)
                continue;
            
            if (RectsOverlap(
                x, y, GameConstants.BulletSize, GameConstants.BulletSize,
                tank.X, tank.Y, GameConstants.TankSize, GameConstants.TankSize))
            {
                return tank;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Check if a position is out of map bounds
    /// </summary>
    public static bool IsOutOfBounds(double x, double y, double width, double height, GameRoomState room)
    {
        var worldWidth = room.MapWidth * GameConstants.TileSize;
        var worldHeight = room.MapHeight * GameConstants.TileSize;
        
        return x < 0 || y < 0 || x + width > worldWidth || y + height > worldHeight;
    }
    
    /// <summary>
    /// Try to move a tank, checking for collisions. Returns the new valid position.
    /// </summary>
    public static (double newX, double newY, bool moved) TryMoveTank(
        TankState tank, 
        Direction direction, 
        GameRoomState room)
    {
        var (dx, dy) = direction.ToVector();
        var newX = tank.X + dx * GameConstants.TankSpeed;
        var newY = tank.Y + dy * GameConstants.TankSpeed;
        
        // Check bounds
        var worldWidth = room.MapWidth * GameConstants.TileSize;
        var worldHeight = room.MapHeight * GameConstants.TileSize;
        
        newX = Math.Clamp(newX, 0, worldWidth - GameConstants.TankSize);
        newY = Math.Clamp(newY, 0, worldHeight - GameConstants.TankSize);
        
        // Check map collision
        if (TankCollidesWithMap(newX, newY, room))
        {
            return (tank.X, tank.Y, false);
        }
        
        // Check tank collision
        if (TankCollidesWithTank(newX, newY, tank.ConnectionId, room))
        {
            return (tank.X, tank.Y, false);
        }
        
        return (newX, newY, true);
    }
}
