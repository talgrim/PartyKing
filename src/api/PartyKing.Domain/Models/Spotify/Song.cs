namespace PartyKing.Domain.Models.Spotify;

public class Song
{
    public required string Id { get; set; }
    public required string Uri { get; set; }
    public required string Name { get; set; }
    public required IReadOnlyCollection<Artist> Artists { get; set; } = [];
    public required Album Album { get; set; }
    public string? AlbumArtUrl { get; set; }
    public int DurationMs { get; set; }

    public string ArtistsDisplay => string.Join(", ", Artists.Select(x => x.Name));

    public string ToDisplayString()
    {
        return $"{ArtistsDisplay} - {Name} - {Album.Name}";
    }
}
