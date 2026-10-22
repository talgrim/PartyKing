namespace PartyKing.Domain.Models.Spotify;

public class Album
{
    public required string Name { get; set; }
    public required IReadOnlyCollection<Artist> Artists { get; set; } = [];
}