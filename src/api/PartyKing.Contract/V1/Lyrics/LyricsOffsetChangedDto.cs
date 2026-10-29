namespace PartyKing.Contract.V1.Lyrics;

/// <param name="SongId">The queue item that was playing when the offset was saved.</param>
public record LyricsOffsetChangedDto(int SongId, int OffsetMs);
