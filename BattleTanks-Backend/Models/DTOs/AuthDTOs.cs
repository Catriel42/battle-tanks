namespace BattleTanks_Backend.Models.DTOs;

public record RegisterRequest(string Username, string Email, string Password);
public record LoginRequest(string Username, string Password);
public record AuthResponse(string Token, Guid PlayerId, string Username);
