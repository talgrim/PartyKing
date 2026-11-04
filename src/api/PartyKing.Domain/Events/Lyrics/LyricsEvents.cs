namespace PartyKing.Domain.Events.Lyrics;

/// <summary>
/// The timing offset of a song was saved. The offset is global, but only <paramref name="SessionId"/>, whose host or
/// admin saved it while <paramref name="SongId"/> was playing, is told.
/// </summary>
public sealed record LyricsOffsetChanged(Guid SessionId, int SongId, string SongUrl, int OffsetMs) : IDomainEvent;
