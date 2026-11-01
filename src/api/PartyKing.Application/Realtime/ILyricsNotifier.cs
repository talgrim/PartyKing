using PartyKing.Contract.V1.Lyrics;

namespace PartyKing.Application.Realtime;

/// <summary>Pushes lyrics changes to a session.</summary>
public interface ILyricsNotifier
{
    /// <summary>The host or an admin saved a new lyrics timing offset for the song being played.</summary>
    Task LyricsOffsetChangedAsync(Guid sessionId, LyricsOffsetChangedDto offset, CancellationToken cancellationToken);
}
