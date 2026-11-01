using PartyKing.Application.Events;
using PartyKing.Application.Queue.Services;
using PartyKing.Domain.Events.Queue;

namespace PartyKing.Application.Queue.Handlers;

/// <summary>
/// Keeps "Up next" from running dry: queues the playlist song that follows the one now starting, behind whatever
/// guests add meanwhile, and replaces a removed song the playlist had queued. The dispatcher runs this after the
/// session's lock is released, which is why <see cref="IFallbackQueueFiller"/> may write to the queue again.
/// </summary>
internal sealed class FallbackRefillHandler :
    IDomainEventHandler<QueueAdvanced>,
    IDomainEventHandler<SongRemoved>
{
    private readonly IFallbackQueueFiller _filler;

    public FallbackRefillHandler(IFallbackQueueFiller filler)
    {
        _filler = filler;
    }

    public Task HandleAsync(QueueAdvanced domainEvent, CancellationToken cancellationToken)
    {
        return _filler.EnsureQueuedAsync(domainEvent.SessionId, cancellationToken);
    }

    public Task HandleAsync(SongRemoved domainEvent, CancellationToken cancellationToken)
    {
        return _filler.EnsureQueuedAsync(domainEvent.SessionId, cancellationToken);
    }
}
