using PartyKing.Application.Events;
using PartyKing.Domain.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Enums;
using PartyKing.Domain.Events.Sessions;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

internal class SessionsRepository : ISessionsRepository
{
    private const int SqliteConstraintErrorCode = 19;

    private readonly IDbContextFactory<ReaderDbContext> _readerDbContextFactory;
    private readonly IDbContextFactory<WriterDbContext> _writerDbContextFactory;
    private readonly IDomainEventDispatcher _dispatcher;

    public SessionsRepository(
        IDbContextFactory<ReaderDbContext> readerDbContextFactory,
        IDbContextFactory<WriterDbContext> writerDbContextFactory,
        IDomainEventDispatcher dispatcher)
    {
        _readerDbContextFactory = readerDbContextFactory;
        _writerDbContextFactory = writerDbContextFactory;
        _dispatcher = dispatcher;
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

    public Task<bool> RenameAsync(Guid id, string name, CancellationToken cancellationToken)
    {
        return UpdateAsync(id, session => session.Rename(name), cancellationToken);
    }

    public Task<bool> UpdatePasswordHashAsync(
        Guid id,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        return UpdateAsync(id, session => session.ChangePassword(passwordHash), cancellationToken);
    }

    public Task<bool> SetFallbackPlaylistAsync(
        Guid id,
        string? playlistId,
        string? playlistName,
        bool shuffle,
        CancellationToken cancellationToken)
    {
        return UpdateAsync(
            id,
            session =>
            {
                if (playlistId is null)
                {
                    session.ClearFallbackPlaylist();
                }
                else
                {
                    session.SetFallbackPlaylist(playlistId, playlistName, shuffle);
                }
            },
            cancellationToken);
    }

    public async Task<bool> SetFallbackCursorAsync(Guid id, int cursor, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

        // Bookkeeping of the refill, not a change anyone reacts to, so it is a plain update that raises nothing
        var updated = await dbContext.Sessions
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.FallbackCursor, cursor), cancellationToken);

        return updated > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

        // The row is gone after the delete, so what the cleanup needs is read first
        var session = await dbContext.Sessions
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.HostProfileId, x.FallbackPlaylistId })
            .FirstOrDefaultAsync(cancellationToken);

        // Queue items and pending OAuth states are removed by the cascade delete
        var deleted = await dbContext.Sessions
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
        {
            return false;
        }

        // Nothing is loaded for an ExecuteDelete, so the repository raises the event, like LobbyGamesRepository.DeleteAsync
        await _dispatcher.DispatchAsync(
            [new SessionDeleted(id, session?.HostProfileId, session?.FallbackPlaylistId)],
            CancellationToken.None);

        return true;
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

            if (updated == 0)
            {
                return SessionClaimResult.AlreadyClaimed;
            }

            // The claim is a conditional update on an untracked row, so the repository raises the event
            await _dispatcher.DispatchAsync([new HostClaimed(id, profileId)], CancellationToken.None);

            return SessionClaimResult.Claimed;
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == SqliteConstraintErrorCode)
        {
            // Unique index on HostProfileId: this account already hosts another session
            return SessionClaimResult.ProfileHostsAnotherSession;
        }
    }

    public Task<bool> ReleaseAsync(Guid id, CancellationToken cancellationToken)
    {
        return UpdateAsync(id, session => session.Release(), cancellationToken);
    }

    /// <summary>
    /// Loads the session, runs a Domain action on it, saves, and hands the events it raised to the dispatcher once the
    /// change is committed. Returns false when the session does not exist (also when it vanished under the save).
    /// </summary>
    private async Task<bool> UpdateAsync(Guid id, Action<Session> action, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

        var session = await dbContext.Sessions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (session is null)
        {
            return false;
        }

        action(session);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Deleted between the load and the save
            return false;
        }

        // CancellationToken.None: the change is committed, an aborted request must not drop the cleanup
        await _dispatcher.DispatchAsync(session.DrainEvents(), CancellationToken.None);

        return true;
    }
}
