using Microsoft.EntityFrameworkCore;
using PartyKing.Application.Events;
using PartyKing.Domain.Entities.Lyrics;
using PartyKing.Domain.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

internal class LyricsOffsetsRepository : ILyricsOffsetsRepository
{
    // The second attempt finds the row the racing save inserted; rows are never deleted, so a third is never needed
    private const int MaxAttempts = 2;

    private readonly IDbContextFactory<ReaderDbContext> _readerFactory;
    private readonly IDbContextFactory<WriterDbContext> _writerFactory;
    private readonly IDomainEventDispatcher _dispatcher;

    public LyricsOffsetsRepository(
        IDbContextFactory<ReaderDbContext> readerFactory,
        IDbContextFactory<WriterDbContext> writerFactory,
        IDomainEventDispatcher dispatcher)
    {
        _readerFactory = readerFactory;
        _writerFactory = writerFactory;
        _dispatcher = dispatcher;
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

    public async Task<LyricsOffsetFailure> ChangeAsync(
        string songUrl,
        int offsetMs,
        Guid sessionId,
        int songId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

            var offset = await dbContext.LyricsOffsets.FirstOrDefaultAsync(x => x.SongUrl == songUrl, cancellationToken);
            var isNew = offset is null;
            offset ??= new LyricsOffset(songUrl);

            var failure = offset.Change(offsetMs, sessionId, songId);
            if (failure != LyricsOffsetFailure.None)
            {
                return failure;
            }

            if (isNew)
            {
                dbContext.LyricsOffsets.Add(offset);
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (isNew && ex.IsUniqueViolation() && attempt < MaxAttempts)
            {
                // Another save inserted the song's row between the read and ours: start again on top of it
                continue;
            }

            // Not tied to the request: an aborted request must not drop the push of a change that is saved
            await _dispatcher.DispatchAsync(offset.DrainEvents(), CancellationToken.None);

            return LyricsOffsetFailure.None;
        }
    }
}
