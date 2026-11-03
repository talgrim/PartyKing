namespace PartyKing.Contract.V1.Queue;

/// <param name="Input">A YouTube URL, a Spotify track link or a spotify:track: URI.</param>
public record EnqueueSongRequestDto(string Input);
