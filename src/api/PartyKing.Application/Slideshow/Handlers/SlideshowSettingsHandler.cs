using PartyKing.Application.Events;
using PartyKing.Application.Slideshow.Services;
using PartyKing.Domain.Events.Slideshow;

namespace PartyKing.Application.Slideshow.Handlers;

/// <summary>A new slide time applies to the slide on display right away, counted from now.</summary>
internal sealed class SlideshowSettingsHandler : IDomainEventHandler<SlideshowSettingsChanged>
{
    private readonly ISlideshowService _slideshowService;

    public SlideshowSettingsHandler(ISlideshowService slideshowService)
    {
        _slideshowService = slideshowService;
    }

    public Task HandleAsync(SlideshowSettingsChanged domainEvent, CancellationToken cancellationToken)
    {
        return _slideshowService.RestartSlideAsync(domainEvent.SessionId, cancellationToken);
    }
}
