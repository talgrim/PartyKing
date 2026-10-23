namespace PartyKing.Contract.V1.Spotify;

public record SpotifyTrackDto(string Uri, string Name, string Artists, string? AlbumArtUrl, int DurationMs);
