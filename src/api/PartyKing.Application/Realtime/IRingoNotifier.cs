using PartyKing.Application.Games.Services;

namespace PartyKing.Application.Realtime;

/// <summary>
/// Pushes Ringo changes to a session: <c>OpenedAsync</c> when an admin opens the game to guests, <c>ChangedAsync</c> when
/// it was started, flipped, continued, joined, left or ended.
/// </summary>
public interface IRingoNotifier : IGameLobbyNotifier;
