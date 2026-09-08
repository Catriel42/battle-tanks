using BattleTanks_Backend.Data;
using BattleTanks_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BattleTanks_Backend.Controllers;

[ApiController]
[Route("api/maps")]
[Authorize]
public class MapController : ControllerBase
{
    private readonly IDbContextFactory _dbFactory;

    public MapController(IDbContextFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public record MapResponse(Guid Id, string Name, int Width, int Height);
    public record MapDetailResponse(Guid Id, string Name, int Width, int Height, string TileData);

    [HttpGet]
    public async Task<IActionResult> GetMaps()
    {
        await using var replicaContext = _dbFactory.CreateReplicaContext();
        
        var maps = await replicaContext.Maps
            .AsNoTracking()
            .Select(m => new MapResponse(m.Id, m.Name, m.Width, m.Height))
            .ToListAsync();

        return Ok(maps);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetMap(Guid id)
    {
        await using var replicaContext = _dbFactory.CreateReplicaContext();
        
        var map = await replicaContext.Maps
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);
            
        if (map == null) return NotFound("Map not found.");

        return Ok(new MapDetailResponse(map.Id, map.Name, map.Width, map.Height, map.TileData));
    }
}
