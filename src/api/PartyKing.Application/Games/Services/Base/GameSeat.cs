namespace PartyKing.Application.Games.Services.Base;

/// <summary>What a device's player cookie holds: the player and the stamp of the seat (null until it was taken over).</summary>
public readonly record struct GameSeat(Guid PlayerId, Guid? Stamp);
