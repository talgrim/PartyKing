namespace PartyKing.Contract.V1.Spotify;

/// <param name="Uri">spotify:playlist:... URI, accepted by the fallback playlist endpoint.</param>
public record PlaylistDto(string Uri, string Name, string? ImageUrl, int TrackCount);
