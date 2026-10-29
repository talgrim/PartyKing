namespace PartyKing.Contract.V1.Lyrics;

/// <param name="OffsetMs">Positive shows the lyrics earlier, negative later.</param>
public record SetLyricsOffsetRequest(int OffsetMs);
