using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Lyrics;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

public interface ILyricsOffsetsRepository
{
    /// <summary>The saved offset of the song, or 0 when none was saved.</summary>
    Task<int> GetAsync(string songUrl, CancellationToken cancellationToken);

    Task SaveAsync(string songUrl, int offsetMs, CancellationToken cancellationToken);
}

internal class LyricsOffsetsRepository : ILyricsOffsetsRepository
{
    private readonly IDbContextFactory<ReaderDbContext> _readerFactory;
    private readonly IDbContextFactory<WriterDbContext> _writerFactory;

    public LyricsOffsetsRepository(
        IDbContextFactory<ReaderDbContext> readerFactory,
        IDbContextFactory<WriterDbContext> writerFactory)
    {
        _readerFactory = readerFactory;
        _writerFactory = writerFactory;
    }

    public async Task<int> GetAsync(string songUrl, CancellationToken cancellationToken)
    {
        await using var dbContext = await _readerFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.LyricsOffsets
            .AsNoTracking()
            .Where(x => x.SongUrl == songUrl)
            .Select(x => x.OffsetMs)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SaveAsync(string songUrl, int offsetMs, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

        var updated = await dbContext.LyricsOffsets
            .Where(x => x.SongUrl == songUrl)
            .ExecuteUpdateAsync(x => x.SetProperty(o => o.OffsetMs, offsetMs), cancellationToken);

        if (updated == 0)
        {
            dbContext.LyricsOffsets.Add(new LyricsOffset { SongUrl = songUrl, OffsetMs = offsetMs });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
