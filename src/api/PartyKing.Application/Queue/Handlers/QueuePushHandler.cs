using PartyKing.Application.Events;
using PartyKing.Application.Mappings;
using PartyKing.Application.Realtime;
using PartyKing.Domain.Events.Queue;

namespace PartyKing.Application.Queue.Handlers;

/// <summary>Tells the session's clients that its queue changed, whoever changed it: a guest, the host, an admin or the fallback playlist.</summary>
internal sealed class QueuePushHandler :
    IDomainEventHandler<SongQueued>,
    IDomainEventHandler<SongRemoved>,
    IDomainEventHandler<QueueAdvanced>
{
    private readonly IQueueNotifier _notifier;

    public QueuePushHandler(IQueueNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task HandleAsync(SongQueued domainEvent, CancellationToken cancellationToken)
    {
        return _notifier.SongAddedAsync(domainEvent.SessionId, domainEvent.Song.ToDto(), cancellationToken);
    }

    public Task HandleAsync(SongRemoved domainEvent, CancellationToken cancellationToken)
    {
        return _notifier.SongRemovedAsync(domainEvent.SessionId, domainEvent.SongId, cancellationToken);
    }

    public Task HandleAsync(QueueAdvanced domainEvent, CancellationToken cancellationToken)
    {
        return _notifier.QueueAdvancedAsync(
            domainEvent.SessionId,
            domainEvent.PreviousSongId,
            domainEvent.NowPlaying?.ToDto(),
            cancellationToken);
    }
}
