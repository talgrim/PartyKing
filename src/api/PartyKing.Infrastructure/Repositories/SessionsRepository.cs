using PartyKing.Domain.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Enums;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

internal class SessionsRepository : ISessionsRepository
{
    private const int SqliteConstraintErrorCode = 19;

    private readonly IDbContextFactory<ReaderDbContext> _readerDbContextFactory;
    private readonly IDbContextFactory<WriterDbContext> _writerDbContextFactory;

    public SessionsRepository(
        IDbContextFactory<ReaderDbContext> readerDbContextFactory,
        IDbContextFactory<WriterDbContext> writerDbContextFactory)
    {
        _readerDbContextFactory = readerDbContextFactory;
        _writerDbContextFactory = writerDbContextFactory;
    }

    public async Task<bool> TryAddAsync(Session session, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.Sessions.Add(session);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // The only unique constraint a new session can violate is its code
            return false;
        }
    }

    public async Task<IReadOnlyList<Session>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _readerDbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.Sessions
            .AsNoTracking()
            .Include(x => x.HostProfile)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Session?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var dbContext = await _readerDbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.Sessions
            .AsNoTracking()
            .Include(x => x.HostProfile)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<bool> RenameAsync(Guid id, string name, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

        var updated = await dbContext.Sessions
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.Name, name), cancellationToken);

        return updated > 0;
    }

    public async Task<bool> UpdatePasswordHashAsync(
        Guid id,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

        var updated = await dbContext.Sessions
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.PasswordHash, passwordHash), cancellationToken);

        return updated > 0;
    }

    public async Task<bool> SetFallbackPlaylistAsync(
        Guid id,
        string? playlistId,
        string? playlistName,
        bool shuffle,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

        var updated = await dbContext.Sessions
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(
                x => x
                    .SetProperty(s => s.FallbackPlaylistId, playlistId)
                    .SetProperty(s => s.FallbackPlaylistName, playlistName)
                    .SetProperty(s => s.FallbackShuffle, shuffle)
                    .SetProperty(s => s.FallbackCursor, 0),
                cancellationToken);

        return updated > 0;
    }

    public async Task<bool> SetFallbackCursorAsync(Guid id, int cursor, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

        var updated = await dbContext.Sessions
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.FallbackCursor, cursor), cancellationToken);

        return updated > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

        // Queue items and pending OAuth states are removed by the cascade delete
        var deleted = await dbContext.Sessions
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted > 0;
    }

    public async Task<SessionClaimResult> TryClaimAsync(
        Guid id,
        string profileId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

        var session = await dbContext.Sessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (session is null)
        {
            return SessionClaimResult.SessionNotFound;
        }

        if (session.HostProfileId == profileId)
        {
            // The host logging in again keeps their session
            return SessionClaimResult.Claimed;
        }

        if (session.HostProfileId is not null)
        {
            return SessionClaimResult.AlreadyClaimed;
        }

        try
        {
            // Conditional update: of two simultaneous claims only one matches HostProfileId IS NULL
            var updated = await dbContext.Sessions
                .Where(x => x.Id == id && x.HostProfileId == null)
                .ExecuteUpdateAsync(x => x.SetProperty(s => s.HostProfileId, profileId), cancellationToken);

            return updated > 0 ? SessionClaimResult.Claimed : SessionClaimResult.AlreadyClaimed;
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == SqliteConstraintErrorCode)
        {
            // Unique index on HostProfileId: this account already hosts another session
            return SessionClaimResult.ProfileHostsAnotherSession;
        }
    }

    public async Task<bool> ReleaseAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

        var updated = await dbContext.Sessions
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(
                x => x
                    .SetProperty(s => s.HostProfileId, (string?)null)
                    // The playlist belongs to the host who chose it, so the next host starts without one
                    .SetProperty(s => s.FallbackPlaylistId, (string?)null)
                    .SetProperty(s => s.FallbackPlaylistName, (string?)null)
                    .SetProperty(s => s.FallbackShuffle, false)
                    .SetProperty(s => s.FallbackCursor, 0),
                cancellationToken);

        return updated > 0;
    }
}
