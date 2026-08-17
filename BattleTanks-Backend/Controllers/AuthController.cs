using BattleTanks_Backend.Data;
using BattleTanks_Backend.Models.DTOs;
using BattleTanks_Backend.Models.Entities;
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

    public AuthController(BattleTanksDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
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

        var token = GenerateJwtToken(player);
        return Ok(new AuthResponse(token, player.Id, player.Username));
    }

    private string GenerateJwtToken(Player player)
    {
        var jwtSecret = _configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret is missing");
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, player.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, player.Username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
