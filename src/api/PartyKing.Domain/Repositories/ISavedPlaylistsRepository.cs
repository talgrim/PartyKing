using PartyKing.Domain.Entities.Playlists;

namespace PartyKing.Domain.Repositories;

public interface ISavedPlaylistsRepository
{
    /// <summary>How many playlists the account has saved.</summary>
    Task<int> CountByOwnerAsync(Guid ownerAccountId, CancellationToken cancellationToken);

    /// <summary>Stores the playlist with all of its songs.</summary>
    Task AddAsync(SavedPlaylist playlist, CancellationToken cancellationToken);
}
