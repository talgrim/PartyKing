using PartyKing.Domain.Entities.Queue;

namespace PartyKing.Domain.Entities.Playlists;

public enum SavedPlaylistFailure
{
    None,
    NameEmpty,
    NameTooLong,
    NoSongs,
    TooManySongs,
    TooManyPlaylists
}

/// <summary>A playlist that was made, or the reason it was not.</summary>
public readonly record struct SavedPlaylistCreation(SavedPlaylistFailure Failure, SavedPlaylist? Playlist);

/// <summary>A playlist an account saved from the songs of a session. It belongs to the account, not to the session.</summary>
public class SavedPlaylist
{
    public const int MaxNameLength = 60;
    public const int MaxSongs = 500;
    public const int MaxPerAccount = 50;

    private SavedPlaylist()
    {
    }

    public Guid Id { get; internal set; }
    public Guid OwnerAccountId { get; internal set; }
    public string Name { get; internal set; } = null!;
    public DateTime CreatedAt { get; internal set; }
    public List<SavedPlaylistItem> Items { get; internal set; } = [];

    /// <summary>
    /// Makes a playlist under the trimmed name from the songs, in the order given. A blank or too long name, no songs,
    /// too many songs, or an owner who has <see cref="MaxPerAccount"/> playlists already is refused. The id and the
    /// time are arguments, like in the sessions.
    /// </summary>
    public static SavedPlaylistCreation Create(
        Guid id,
        Guid ownerAccountId,
        string? name,
        IReadOnlyList<QueueItem> songs,
        int ownerPlaylistCount,
        DateTime now)
    {
        var failure = Validate(name, songs.Count, ownerPlaylistCount);

        if (failure != SavedPlaylistFailure.None)
        {
            return new SavedPlaylistCreation(failure, null);
        }

        var playlist = new SavedPlaylist
        {
            Id = id,
            OwnerAccountId = ownerAccountId,
            Name = name!.Trim(),
            CreatedAt = now,
            Items = songs.Select((song, position) => new SavedPlaylistItem(position, song)).ToList()
        };

        return new SavedPlaylistCreation(SavedPlaylistFailure.None, playlist);
    }

    private static SavedPlaylistFailure Validate(string? name, int songCount, int ownerPlaylistCount)
    {
        var trimmed = name?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return SavedPlaylistFailure.NameEmpty;
        }

        if (trimmed.Length > MaxNameLength)
        {
            return SavedPlaylistFailure.NameTooLong;
        }

        if (songCount == 0)
        {
            return SavedPlaylistFailure.NoSongs;
        }

        if (songCount > MaxSongs)
        {
            return SavedPlaylistFailure.TooManySongs;
        }

        return ownerPlaylistCount >= MaxPerAccount
            ? SavedPlaylistFailure.TooManyPlaylists
            : SavedPlaylistFailure.None;
    }
}
