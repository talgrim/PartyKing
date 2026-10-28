using PartyKing.Contract.V1.Enums;

namespace PartyKing.Contract.V1.Queue;

/// <param name="Url">YouTube URL, or Spotify track URI (spotify:track:...).</param>
public record QueueItemDto(
    int Id,
    SongSource Source,
    string Url,
    string Name,
    string? Artists,
    string? AlbumArtUrl,
    int? DurationMs,
    bool IsFallback = false);
