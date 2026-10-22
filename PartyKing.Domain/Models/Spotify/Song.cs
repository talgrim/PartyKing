namespace PartyKing.Domain.Models.Spotify;

public class Song
{
    public required string Name { get; set; }
    public required IReadOnlyCollection<Artist> Artists { get; set; } = [];
    public required Album Album { get; set; }

    public string ToDisplayString()
    {
        return $"{string.Join(", ", Artists.Select(x => x.Name))} - {Name} - {Album.Name}";
    }
}