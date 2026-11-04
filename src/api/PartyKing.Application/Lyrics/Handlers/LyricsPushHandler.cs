using PartyKing.Application.Events;
using PartyKing.Application.Realtime;
using PartyKing.Contract.V1.Lyrics;
using PartyKing.Domain.Events.Lyrics;

namespace PartyKing.Application.Lyrics.Handlers;

/// <summary>Tells the viewers of the session that saved a song's timing offset what it is now.</summary>
internal sealed class LyricsPushHandler : IDomainEventHandler<LyricsOffsetChanged>
{
    private readonly ILyricsNotifier _notifier;

    public LyricsPushHandler(ILyricsNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task HandleAsync(LyricsOffsetChanged domainEvent, CancellationToken cancellationToken)
    {
        return _notifier.LyricsOffsetChangedAsync(
            domainEvent.SessionId,
            new LyricsOffsetChangedDto(domainEvent.SongId, domainEvent.OffsetMs),
            cancellationToken);
    }
}
