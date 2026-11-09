using ErrorOr;
using PartyKing.Domain.Entities.Playlists;

namespace PartyKing.Application.Playlists.Helpers;

/// <summary>Maps the failures of the Domain's saved playlist rules to errors.</summary>
internal static class SavedPlaylistErrors
{
    public static Error From(SavedPlaylistFailure failure)
    {
        return failure switch
        {
            SavedPlaylistFailure.NameTooLong => Error.Validation(
                description: $"The name must be at most {SavedPlaylist.MaxNameLength} characters"),
            SavedPlaylistFailure.NoSongs => Error.Validation(
                description: "There are no songs to save in the chosen parts of the queue"),
            SavedPlaylistFailure.TooManySongs => Error.Validation(
                description: $"A playlist can hold at most {SavedPlaylist.MaxSongs} songs"),
            SavedPlaylistFailure.TooManyPlaylists => Error.Validation(
                description: $"You can save at most {SavedPlaylist.MaxPerAccount} playlists"),
            _ => Error.Validation(description: "The name must not be blank")
        };
    }
}
