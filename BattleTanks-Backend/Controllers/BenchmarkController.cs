using BattleTanks_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BattleTanks_Backend.Controllers;

[ApiController]
[Route("api/benchmark")]
[Authorize]
public class BenchmarkController : ControllerBase
{
    private readonly QueryBenchmarkService _benchmarkService;

    public BenchmarkController(QueryBenchmarkService benchmarkService)
    {
        _benchmarkService = benchmarkService;
    }

    [HttpGet]
    public async Task<IActionResult> RunBenchmarks([FromQuery] Guid? playerId = null)
    {
        var results = await _benchmarkService.RunAllBenchmarks(playerId);
        
        return Ok(new
        {
            Timestamp = DateTime.UtcNow,
            Results = results.Select(r => new
            {
                r.QueryName,
                ElapsedMs = r.ElapsedMs,
                r.ResultCount
            })
        });
    }
}
