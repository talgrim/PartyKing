using PartyKing.Application.Sessions;

namespace PartyKing.Application.Games.Services.Base;

/// <summary>
/// Who calls: the host's Spotify profile id, whether an admin, and the player id of the caller's device with the
/// stamp its cookie carries (see <c>LobbyPlayer.ClaimStamp</c>); the id counts only when the stamp is the seat's.
/// </summary>
public record GameCaller(string? ProfileId, bool IsAdmin, Guid? PlayerId = null, Guid? PlayerStamp = null)
    : SessionCaller(ProfileId, IsAdmin);
