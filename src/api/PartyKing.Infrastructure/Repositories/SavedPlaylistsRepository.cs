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

    public async Task<IReadOnlyList<SavedPlaylistSummary>> ListByOwnerAsync(
        Guid ownerAccountId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _readerDbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.SavedPlaylists
            .Where(x => x.OwnerAccountId == ownerAccountId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Name)
            .Select(x => new SavedPlaylistSummary(x.Id, x.Name, x.CreatedAt, x.Items.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<SavedPlaylist?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var dbContext = await _readerDbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.SavedPlaylists
            .AsNoTracking()
            .Include(x => x.Items.OrderBy(item => item.Position))
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddAsync(SavedPlaylist playlist, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.SavedPlaylists.Add(playlist);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
