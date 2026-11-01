using Microsoft.AspNetCore.SignalR;
using PartyKing.Application.Realtime;

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
}
