using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Enums;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

public interface ISessionsRepository
{
    /// <summary>Returns false when the session code is already taken.</summary>
    Task<bool> TryAddAsync(Session session, CancellationToken cancellationToken);
    Task<IReadOnlyList<Session>> GetAllAsync(CancellationToken cancellationToken);
    Task<Session?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> RenameAsync(Guid id, string name, CancellationToken cancellationToken);
    Task<bool> UpdatePasswordHashAsync(Guid id, string passwordHash, CancellationToken cancellationToken);

    /// <summary>Deletes the session together with its queue.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Atomically assigns the host. The first profile to claim a session owns it.</summary>
    Task<SessionClaimResult> TryClaimAsync(Guid id, string profileId, CancellationToken cancellationToken);
    Task<bool> ReleaseAsync(Guid id, CancellationToken cancellationToken);
}

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
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.HostProfileId, (string?)null), cancellationToken);

        return updated > 0;
    }
}
