using PartyKing.Application.Games.Services.Base;

namespace PartyKing.Application.Realtime;

/// <summary>
/// Pushes Resistance changes to a session: <c>OpenedAsync</c> when an admin opens the game to guests,
/// <c>ChangedAsync</c> when anything about the game changed (every action of a round), <c>LobbyChangedAsync</c> when
/// what the lobby shows changed (the game exists, opens, starts, ends or someone joins or is kicked), which is much
/// rarer, so the clients that only show the lobby listen to it alone.
/// </summary>
public interface IResistanceNotifier : IGameLobbyNotifier
{
    Task LobbyChangedAsync(Guid sessionId, CancellationToken cancellationToken);
}
