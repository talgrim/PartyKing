using Microsoft.AspNetCore.SignalR;
using PartyKing.Application.Realtime;

namespace PartyKing.API.Hubs.Notifiers;

internal sealed class SignalRRingoNotifier : SessionGroupNotifier, IRingoNotifier
{
    public SignalRRingoNotifier(IHubContext<SessionHub> hub) : base(hub, SessionHubTopic.Ringo)
    {
    }

    public Task OpenedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return Clients(sessionId).SendAsync(SessionMusicHubEvents.RingoOpened, cancellationToken);
    }

    public Task ChangedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return Clients(sessionId).SendAsync(SessionMusicHubEvents.RingoChanged, cancellationToken);
    }
}
