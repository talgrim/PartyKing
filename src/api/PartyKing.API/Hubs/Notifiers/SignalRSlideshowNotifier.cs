using Microsoft.AspNetCore.SignalR;
using PartyKing.Application.Realtime;

namespace PartyKing.API.Hubs.Notifiers;

internal sealed class SignalRSlideshowNotifier : SessionGroupNotifier, ISlideshowNotifier
{
    public SignalRSlideshowNotifier(IHubContext<SessionHub> hub) : base(hub, SessionHubTopic.Slideshow)
    {
    }

    public Task SlideshowChangedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return Clients(sessionId).SendAsync(SessionMusicHubEvents.SlideChanged, cancellationToken);
    }
}
