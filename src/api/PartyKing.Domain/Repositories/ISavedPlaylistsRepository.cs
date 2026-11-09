using PartyKing.Domain.Entities.Playlists;

namespace PartyKing.Domain.Repositories;

public interface ISavedPlaylistsRepository
{
    /// <summary>How many playlists the account has saved.</summary>
    Task<int> CountByOwnerAsync(Guid ownerAccountId, CancellationToken cancellationToken);

    /// <summary>The playlists of the account without their songs, the newest first.</summary>
    Task<IReadOnlyList<SavedPlaylistSummary>> ListByOwnerAsync(Guid ownerAccountId, CancellationToken cancellationToken);

    /// <summary>The playlist with its songs in their saved order, or null when there is none.</summary>
    Task<SavedPlaylist?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Stores the playlist with all of its songs.</summary>
    Task AddAsync(SavedPlaylist playlist, CancellationToken cancellationToken);
}
