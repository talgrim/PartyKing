namespace PartyKing.Contract.V1.Lyrics;

/// <param name="TimeMs">When the line starts being sung, from the start of the song.</param>
public record LyricsLineDto(int TimeMs, string Text);

/// <param name="SongId">The queue item the lyrics belong to, so a client can drop lyrics for a song that has already ended.</param>
/// <param name="Lines">Ordered by time.</param>
public record LyricsDto(int SongId, IReadOnlyList<LyricsLineDto> Lines);
