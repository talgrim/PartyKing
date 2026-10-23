namespace PartyKing.Domain.Entities;

public class Session
{
    public const string DefaultName = "New session";

    private Session()
    {
    }

    public Session(string code, string name)
    {
        Id = Guid.NewGuid();
        Code = code;
        Name = string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim();
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; set; }

    /// <summary>Short, human-friendly, unique code used to find the session.</summary>
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;

    /// <summary>Spotify profile id of the host; null until someone claims the session.</summary>
    public string? HostProfileId { get; set; }
    public SpotifyProfile? HostProfile { get; set; }
    public DateTime CreatedAt { get; set; }
}
