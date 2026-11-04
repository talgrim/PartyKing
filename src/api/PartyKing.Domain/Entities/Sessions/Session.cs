using System.Security.Cryptography;
using System.Text;
using PartyKing.Domain.Entities.Base;
using PartyKing.Domain.Entities.Spotify;
using PartyKing.Domain.Enums;
using PartyKing.Domain.Events.Sessions;

namespace PartyKing.Domain.Entities.Sessions;

/// <summary>A session that was created, or the reason it was not.</summary>
public readonly record struct SessionCreation(SessionNameFailure Failure, Session? Session);

/// <summary>
/// The track the fallback playlist wants next. <see cref="NextCursor"/> is the cursor to store after queueing it,
/// null when the playlist is shuffled (the cursor is not used then).
/// </summary>
public sealed record FallbackPick(Song Track, int? NextCursor);

public class Session : AggregateRoot
{
    public const string DefaultName = SessionNames.DefaultName;

    private Session()
    {
    }

    /// <summary>The name is already normalized and checked by <see cref="Create"/>.</summary>
    internal Session(Guid id, string code, string name, string passwordHash, DateTime createdAt)
    {
        Id = id;
        Code = code;
        PasswordHash = passwordHash;
        Name = name;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Makes a session. A blank name falls back to <see cref="DefaultName"/>; a name over
    /// <see cref="SessionNames.MaxLength"/> characters is refused. Id and time are arguments, like in the games.
    /// </summary>
    public static SessionCreation Create(Guid id, string code, string? name, string passwordHash, DateTime now)
    {
        var normalized = SessionNames.Normalize(name);

        if (SessionNames.Validate(normalized) == SessionNameFailure.TooLong)
        {
            return new SessionCreation(SessionNameFailure.TooLong, null);
        }

        return new SessionCreation(SessionNameFailure.None, new Session(id, code, normalized, passwordHash, now));
    }

    public Guid Id { get; internal set; }

    /// <summary>Short, human-friendly, unique code used to find the session.</summary>
    public string Code { get; internal set; } = null!;
    public string Name { get; internal set; } = null!;

    /// <summary>Hash of the password everyone (admins included) must enter to join the session.</summary>
    public string PasswordHash { get; internal set; } = null!;

    /// <summary>
    /// Proof that the password was entered, kept in the visitor's cookie. It derives from the password hash, so it
    /// changes with the password and the unlocks issued for the old one stop working.
    /// </summary>
    public string AccessStamp => AccessStampOf(PasswordHash);

    /// <summary>Spotify profile id of the host; null until someone claims the session.</summary>
    public string? HostProfileId { get; internal set; }

    /// <summary>
    /// The host's name on Spotify, for display. Not stored with the session and not the profile itself (which holds the
    /// refresh token): a repository that reads sessions fills it with <see cref="WithHostDisplayName"/>.
    /// </summary>
    public string? HostDisplayName { get; internal set; }

    /// <summary>Spotify id of the host's playlist played whenever the queue runs empty; null when there is none.</summary>
    public string? FallbackPlaylistId { get; internal set; }
    public string? FallbackPlaylistName { get; internal set; }

    /// <summary>Whether the fallback playlist is shuffled instead of played in order.</summary>
    public bool FallbackShuffle { get; internal set; }

    /// <summary>Index in the playlist of the next track to queue when it is played in order.</summary>
    public int FallbackCursor { get; internal set; }
    public DateTime CreatedAt { get; internal set; }

    /// <summary>The stamp a session with this password hash has, for callers that only know the new hash.</summary>
    public static string AccessStampOf(string passwordHash)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash)))[..16];
    }

    /// <summary>Whether <paramref name="accessStamp"/> is the current <see cref="AccessStamp"/>.</summary>
    public bool IsUnlockedBy(string? accessStamp)
    {
        return !string.IsNullOrEmpty(accessStamp) && accessStamp == AccessStamp;
    }

    /// <summary>Whether <paramref name="profileId"/> is the session's host. A session without a host has no such profile.</summary>
    public bool IsHostedBy(string? profileId)
    {
        return profileId is not null && HostProfileId == profileId;
    }

    /// <summary>Whether the caller may change the session's settings: the host or an admin.</summary>
    public bool IsManagedBy(string? profileId, bool isAdmin)
    {
        return isAdmin || IsHostedBy(profileId);
    }

    /// <summary>
    /// Renames the session (trimmed, at most <see cref="SessionNames.MaxLength"/> characters, not blank).
    /// A refused name changes nothing and raises nothing.
    /// </summary>
    public SessionNameFailure Rename(string? name)
    {
        var failure = SessionNames.Validate(name);
        if (failure != SessionNameFailure.None)
        {
            return failure;
        }

        Name = name!.Trim();
        Raise(new SessionRenamed(Id, Name));

        return SessionNameFailure.None;
    }

    /// <summary>Replaces the password hash. Every access stamp derives from it, so the old unlocks stop working.</summary>
    public void ChangePassword(string passwordHash)
    {
        PasswordHash = passwordHash;
        Raise(new SessionPasswordChanged(Id));
    }

    /// <summary>
    /// Makes <paramref name="profileId"/> the host of a session that has none and raises <see cref="HostClaimed"/>.
    /// The host logging in again changes nothing, and a session that has another host refuses; neither raises anything.
    /// A caller that persists the claim must do it atomically (first claim wins): this only decides the outcome.
    /// </summary>
    public HostClaimOutcome ClaimHost(string profileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

        if (HostProfileId == profileId)
        {
            return HostClaimOutcome.AlreadyHost;
        }

        if (HostProfileId is not null)
        {
            return HostClaimOutcome.HostedByAnother;
        }

        HostProfileId = profileId;
        Raise(new HostClaimed(Id, profileId));

        return HostClaimOutcome.Claimed;
    }

    /// <summary>Tells the session the display name of its host, as a read of the session found it. Changes nothing stored.</summary>
    public Session WithHostDisplayName(string? displayName)
    {
        HostDisplayName = displayName;

        return this;
    }

    /// <summary>
    /// Removes the host. The playlist belongs to the host who chose it, so the next host starts without one.
    /// Also valid without a host, so the player of a hostless session is still forgotten.
    /// </summary>
    public void Release()
    {
        var previousHost = HostProfileId;

        HostProfileId = null;
        HostDisplayName = null;
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

    /// <summary>
    /// Picks the next track of the fallback playlist, or null when it has none. In order it is the track at
    /// <see cref="FallbackCursor"/> (which wraps around the playlist) and the pick carries the cursor to store next.
    /// Shuffled it is a random track, but not the one that plays (<paramref name="playingUrl"/>) unless it is the only
    /// one. Changes nothing: the caller stores the cursor once the track is really queued.
    /// </summary>
    public FallbackPick? NextFallbackTrack(IReadOnlyList<Song> tracks, string? playingUrl, Random random)
    {
        if (tracks.Count == 0)
        {
            return null;
        }

        if (!FallbackShuffle)
        {
            var index = FallbackCursor % tracks.Count;
            return new FallbackPick(tracks[index], (index + 1) % tracks.Count);
        }

        var candidates = tracks.Where(x => x.Uri != playingUrl).ToList();
        if (candidates.Count == 0)
        {
            candidates = tracks.ToList();
        }

        return new FallbackPick(candidates[random.Next(candidates.Count)], null);
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
