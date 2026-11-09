using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Playlists;
using PartyKing.Domain.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

internal class SavedPlaylistsRepository : ISavedPlaylistsRepository
{
    private readonly IDbContextFactory<ReaderDbContext> _readerDbContextFactory;
    private readonly IDbContextFactory<WriterDbContext> _writerDbContextFactory;

    public SavedPlaylistsRepository(
        IDbContextFactory<ReaderDbContext> readerDbContextFactory,
        IDbContextFactory<WriterDbContext> writerDbContextFactory)
    {
        _readerDbContextFactory = readerDbContextFactory;
        _writerDbContextFactory = writerDbContextFactory;
    }

    public async Task<int> CountByOwnerAsync(Guid ownerAccountId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _readerDbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.SavedPlaylists.CountAsync(x => x.OwnerAccountId == ownerAccountId, cancellationToken);
    }

    public async Task AddAsync(SavedPlaylist playlist, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.SavedPlaylists.Add(playlist);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
