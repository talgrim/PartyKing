using PartyKing.Domain.Entities.Base;
using PartyKing.Domain.Entities.Spotify;
using PartyKing.Domain.Events.Sessions;

namespace PartyKing.Domain.Entities.Sessions;

public class Session : AggregateRoot
{
    public const string DefaultName = SessionNames.DefaultName;

    private Session()
    {
    }

    public Session(string code, string name, string passwordHash)
    {
        Id = Guid.NewGuid();
        Code = code;
        PasswordHash = passwordHash;
        Name = SessionNames.Normalize(name);
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; internal set; }

    /// <summary>Short, human-friendly, unique code used to find the session.</summary>
    public string Code { get; internal set; } = null!;
    public string Name { get; internal set; } = null!;

    /// <summary>Hash of the password everyone (admins included) must enter to join the session.</summary>
    public string PasswordHash { get; internal set; } = null!;

    /// <summary>Spotify profile id of the host; null until someone claims the session.</summary>
    public string? HostProfileId { get; internal set; }
    public SpotifyProfile? HostProfile { get; internal set; }

    /// <summary>Spotify id of the host's playlist played whenever the queue runs empty; null when there is none.</summary>
    public string? FallbackPlaylistId { get; internal set; }
    public string? FallbackPlaylistName { get; internal set; }

    /// <summary>Whether the fallback playlist is shuffled instead of played in order.</summary>
    public bool FallbackShuffle { get; internal set; }

    /// <summary>Index in the playlist of the next track to queue when it is played in order.</summary>
    public int FallbackCursor { get; internal set; }
    public DateTime CreatedAt { get; internal set; }

    /// <summary>Renames the session. The name must pass <see cref="SessionNames.Validate"/>, which callers check first.</summary>
    public void Rename(string? name)
    {
        var failure = SessionNames.Validate(name);
        if (failure != SessionNameFailure.None)
        {
            throw new ArgumentException($"Invalid session name: {failure}", nameof(name));
        }

        Name = name!.Trim();
        Raise(new SessionRenamed(Id, Name));
    }

    /// <summary>Replaces the password hash. Every access stamp derives from it, so the old unlocks stop working.</summary>
    public void ChangePassword(string passwordHash)
    {
        PasswordHash = passwordHash;
        Raise(new SessionPasswordChanged(Id));
    }

    /// <summary>
    /// Removes the host. The playlist belongs to the host who chose it, so the next host starts without one.
    /// Also valid without a host, so the player of a hostless session is still forgotten.
    /// </summary>
    public void Release()
    {
        var previousHost = HostProfileId;

        HostProfileId = null;
        HostProfile = null;
        Raise(new HostReleased(Id, previousHost));

        ClearFallbackPlaylist();
    }

    /// <summary>Sets the fallback playlist and rewinds it. Saving the same playlist again changes the shuffle flag.</summary>
    public void SetFallbackPlaylist(string playlistId, string? playlistName, bool shuffle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playlistId);

        var previous = FallbackPlaylistId;

        FallbackPlaylistId = playlistId;
        FallbackPlaylistName = playlistName;
        FallbackShuffle = shuffle;
        FallbackCursor = 0;
        Raise(new FallbackPlaylistChanged(Id, previous, playlistId));
    }

    /// <summary>Removes the fallback playlist; raises nothing when there is none.</summary>
    public void ClearFallbackPlaylist()
    {
        var previous = FallbackPlaylistId;

        FallbackPlaylistId = null;
        FallbackPlaylistName = null;
        FallbackShuffle = false;
        FallbackCursor = 0;

        if (previous is not null)
        {
            Raise(new FallbackPlaylistCleared(Id, previous));
        }
    }
}
