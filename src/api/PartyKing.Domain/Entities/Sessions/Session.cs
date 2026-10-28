using PartyKing.Domain.Entities.Spotify;

namespace PartyKing.Domain.Entities.Sessions;

public class Session
{
    public const string DefaultName = "New session";

    private Session()
    {
    }

    public Session(string code, string name, string passwordHash)
    {
        Id = Guid.NewGuid();
        Code = code;
        PasswordHash = passwordHash;
        Name = string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim();
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; set; }

    /// <summary>Short, human-friendly, unique code used to find the session.</summary>
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;

    /// <summary>Hash of the password everyone (admins included) must enter to join the session.</summary>
    public string PasswordHash { get; set; } = null!;

    /// <summary>Spotify profile id of the host; null until someone claims the session.</summary>
    public string? HostProfileId { get; set; }
    public SpotifyProfile? HostProfile { get; set; }

    /// <summary>Spotify id of the host's playlist played whenever the queue runs empty; null when there is none.</summary>
    public string? FallbackPlaylistId { get; set; }
    public string? FallbackPlaylistName { get; set; }

    /// <summary>Whether the fallback playlist is shuffled instead of played in order.</summary>
    public bool FallbackShuffle { get; set; }

    /// <summary>Index in the playlist of the next track to queue when it is played in order.</summary>
    public int FallbackCursor { get; set; }
    public DateTime CreatedAt { get; set; }
}
