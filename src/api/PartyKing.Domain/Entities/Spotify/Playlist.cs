namespace PartyKing.Domain.Entities.Spotify;

public class Playlist
{
    public required string Id { get; set; }
    public required string Name { get; set; }

    /// <summary>Spotify profile id of the account that owns the playlist.</summary>
    public required string OwnerId { get; set; }
    public string? ImageUrl { get; set; }
    public int TrackCount { get; set; }
}
