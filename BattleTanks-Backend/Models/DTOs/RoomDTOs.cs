using BattleTanks_Backend.Models.Entities;

namespace BattleTanks_Backend.Models.DTOs;

public record CreateRoomRequest(Guid MapId, int MaxPlayers = 4, int Lives = 3);
public record StartRoomRequest();
public record RoomResponse(
    Guid Id, 
    GameSessionStatus Status, 
    Guid MapId,
    string MapName,
    int MaxPlayers,
    int MinPlayers,
    int Lives,
    int CurrentPlayers,
    bool CanStart
);
