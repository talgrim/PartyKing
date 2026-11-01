using PartyKing.Application.Sessions;

namespace PartyKing.Application.Games.Services.Base;

/// <summary>Who calls: the host's Spotify profile id, whether an admin, and the player id of the caller's device.</summary>
public record GameCaller(string? ProfileId, bool IsAdmin, Guid? PlayerId = null) : SessionCaller(ProfileId, IsAdmin);
