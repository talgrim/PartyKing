using PartyKing.Application.Events;
using PartyKing.Application.Slideshow.Services;
using PartyKing.Domain.Events.Sessions;

namespace PartyKing.Application.Sessions.Handlers;

/// <summary>Removes the session's uploaded photos and its slide in memory; the rows went with the cascade delete.</summary>
internal sealed class SlideshowCleanupHandler : IDomainEventHandler<SessionDeleted>
{
    private readonly ISlideshowService _slideshowService;

    public SlideshowCleanupHandler(ISlideshowService slideshowService)
    {
        _slideshowService = slideshowService;
    }

    public Task HandleAsync(SessionDeleted domainEvent, CancellationToken cancellationToken)
    {
        return _slideshowService.DeleteSessionAsync(domainEvent.SessionId);
    }
}
