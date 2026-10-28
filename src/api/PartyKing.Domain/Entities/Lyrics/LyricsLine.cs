namespace PartyKing.Domain.Entities.Lyrics;

/// <param name="TimeMs">When the line starts being sung, from the start of the song.</param>
public record LyricsLine(int TimeMs, string Text);
