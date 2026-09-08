using BattleTanks_Backend.Data;
using BattleTanks_Backend.Models.DTOs;
using BattleTanks_Backend.Models.Entities;
using BattleTanks_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace BattleTanks_Backend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly BattleTanksDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly JwtSessionService _sessionService;
    private readonly LeaderboardService _leaderboardService;

    public AuthController(
        BattleTanksDbContext context, 
        IConfiguration configuration,
        JwtSessionService sessionService,
        LeaderboardService leaderboardService)
    {
        _context = context;
        _configuration = configuration;
        _sessionService = sessionService;
        _leaderboardService = leaderboardService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (await _context.Players.AnyAsync(p => p.Username == request.Username))
            return BadRequest("Username is already taken.");

        if (await _context.Players.AnyAsync(p => p.Email == request.Email))
            return BadRequest("Email is already registered.");

        var player = new Player
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        _context.Players.Add(player);
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Registration successful" });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var player = await _context.Players.FirstOrDefaultAsync(p => p.Username == request.Username);
        if (player == null || !BCrypt.Net.BCrypt.Verify(request.Password, player.PasswordHash))
        {
            return Unauthorized("Invalid credentials.");
        }

        var (token, jti) = GenerateJwtToken(player);
        
        await _sessionService.StoreSessionAsync(player.Id, jti, token);
        
        await _leaderboardService.UpdatePlayerScoreAsync(
            player.Id, 
            player.Username, 
            player.TotalKills, 
            player.Wins, 
            player.GamesPlayed
        );
        
        return Ok(new AuthResponse(token, player.Id, player.Username));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var playerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var jtiClaim = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        
        if (playerIdClaim == null || !Guid.TryParse(playerIdClaim, out var playerId))
        {
            return Unauthorized();
        }
        
        if (jtiClaim != null)
        {
            await _sessionService.InvalidateSessionAsync(playerId, jtiClaim);
        }
        
        return Ok(new { Message = "Logout successful" });
    }

    [HttpGet("session")]
    [Authorize]
    public async Task<IActionResult> GetSessionInfo()
    {
        var playerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        if (playerIdClaim == null || !Guid.TryParse(playerIdClaim, out var playerId))
        {
            return Unauthorized();
        }
        
        var sessionInfo = await _sessionService.GetSessionInfoAsync(playerId);
        
        if (sessionInfo == null)
        {
            return NotFound(new { Message = "No active session found" });
        }
        
        return Ok(sessionInfo);
    }

    [HttpGet("validate")]
    [Authorize]
    public async Task<IActionResult> ValidateToken()
    {
        var playerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var jtiClaim = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        
        if (playerIdClaim == null || !Guid.TryParse(playerIdClaim, out var playerId) || jtiClaim == null)
        {
            return Unauthorized(new { Valid = false, Message = "Invalid token claims" });
        }
        
        var isValid = await _sessionService.ValidateSessionAsync(playerId, jtiClaim);
        
        if (!isValid)
        {
            return Unauthorized(new { Valid = false, Message = "Session invalid or expired" });
        }
        
        return Ok(new { Valid = true, PlayerId = playerId });
    }

    private (string Token, string Jti) GenerateJwtToken(Player player)
    {
        var jwtSecret = _configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret is missing");
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var jti = Guid.NewGuid().ToString();

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, player.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, player.Username),
            new Claim(JwtRegisteredClaimNames.Jti, jti)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), jti);
    }
}
