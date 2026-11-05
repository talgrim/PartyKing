using Microsoft.AspNetCore.SignalR;
using PartyKing.Application.Realtime;
using PartyKing.Contract.V1.Games.Resistance;

namespace PartyKing.API.Hubs.Notifiers;

internal sealed class SignalRResistanceNotifier : SessionGroupNotifier, IResistanceNotifier
{
    public SignalRResistanceNotifier(IHubContext<SessionHub> hub) : base(hub, SessionHubTopic.Resistance)
    {
    }

    public Task OpenedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return Clients(sessionId).SendAsync(SessionMusicHubEvents.ResistanceOpened, cancellationToken);
    }

    public Task ChangedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return Clients(sessionId).SendAsync(SessionMusicHubEvents.ResistanceChanged, cancellationToken);
    }

    public Task LobbyChangedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return Clients(sessionId, SessionHubTopic.ResistanceLobby)
            .SendAsync(SessionMusicHubEvents.ResistanceLobbyChanged, cancellationToken);
    }

    public Task PresenceChangedAsync(
        Guid sessionId, ResistancePresenceDto presence, CancellationToken cancellationToken)
    {
        return Clients(sessionId, SessionHubTopic.ResistancePresence)
            .SendAsync(SessionMusicHubEvents.ResistancePresenceChanged, presence, cancellationToken);
    }
}
