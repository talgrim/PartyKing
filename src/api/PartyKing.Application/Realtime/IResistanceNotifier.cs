using PartyKing.Application.Games.Services.Base;
using PartyKing.Contract.V1.Games.Resistance;

namespace PartyKing.Application.Realtime;

/// <summary>
/// Pushes Resistance changes to a session: <c>OpenedAsync</c> when an admin opens the game to guests,
/// <c>ChangedAsync</c> when anything about the game changed (every action of a round), <c>LobbyChangedAsync</c> when
/// what the lobby shows changed (the game exists, opens, starts, ends or someone joins or is kicked), which is much
/// rarer, so the clients that only show the lobby listen to it alone. <c>PresenceChangedAsync</c> sends who is
/// connected right now, with the list in the payload, to the TV views only (nobody else follows that topic).
/// </summary>
public interface IResistanceNotifier : IGameLobbyNotifier
{
    Task LobbyChangedAsync(Guid sessionId, CancellationToken cancellationToken);

    Task PresenceChangedAsync(Guid sessionId, ResistancePresenceDto presence, CancellationToken cancellationToken);
}
