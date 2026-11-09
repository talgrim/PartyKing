using PartyKing.Domain.Entities.Playlists;
using PartyKing.Domain.Repositories;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Infrastructure.Metrics;

internal sealed class MeteredSavedPlaylistsRepository : MeteredRepository, ISavedPlaylistsRepository
{
    private readonly ISavedPlaylistsRepository _inner;

    public MeteredSavedPlaylistsRepository(ISavedPlaylistsRepository inner, RepositoryMetrics metrics)
        : base(nameof(SavedPlaylistsRepository), metrics)
    {
        _inner = inner;
    }

    public Task<int> CountByOwnerAsync(Guid ownerAccountId, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(CountByOwnerAsync), () => _inner.CountByOwnerAsync(ownerAccountId, cancellationToken));
    }

    public Task AddAsync(SavedPlaylist playlist, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(AddAsync), () => _inner.AddAsync(playlist, cancellationToken));
    }
}
