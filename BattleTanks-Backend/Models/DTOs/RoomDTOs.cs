using BattleTanks_Backend.Models.Entities;

namespace BattleTanks_Backend.Models.DTOs;

public record CreateRoomRequest(string MapName, int MaxPlayers);
public record JoinRoomRequest(Guid RoomId);
public record RoomResponse(Guid Id, GameSessionStatus Status, string MapName, int MaxPlayers, int CurrentPlayers);
