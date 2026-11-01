using PartyKing.Domain.Repositories;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Games.Base;
using PartyKing.Infrastructure.Metrics;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

/// <summary>
/// The EF Core part every lobby game shares: loading, locking, saving and deleting the game. The rules of the
/// lobby (open, join, kick, reorder) are the game's own, see <see cref="LobbyGame{TPlayer}"/>.
/// </summary>
internal abstract class LobbyGamesRepository<TGame, TPlayer> : ILobbyGamesRepository<TGame, TPlayer>
    where TGame : LobbyGame<TPlayer>
    where TPlayer : LobbyPlayer
{
    private readonly IDbContextFactory<ReaderDbContext> _readerFactory;
    private readonly IDbContextFactory<WriterDbContext> _writerFactory;
    private readonly RepositoryMetrics _metrics;

    // The read in flight per session, shared by every caller that asks for it meanwhile
    private readonly ConcurrentDictionary<Guid, Lazy<Task<TGame?>>> _reads = new();

    // Writes of one session take turns (see WriteAsync); sessions share the locks by hash, as in QueueRepository
    private readonly SemaphoreSlim[] _writeLocks =
        Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    protected LobbyGamesRepository(
        IDbContextFactory<ReaderDbContext> readerFactory,
        IDbContextFactory<WriterDbContext> writerFactory,
        RepositoryMetrics metrics)
    {
        _readerFactory = readerFactory;
        _writerFactory = writerFactory;
        _metrics = metrics;
    }

    /// <summary>The game's table with everything the game needs loaded, including its players.</summary>
    protected abstract IQueryable<TGame> Games(PartyKingDbContextBase dbContext);

    protected abstract DbSet<TPlayer> PlayersOf(PartyKingDbContextBase dbContext);

    protected abstract DbSet<TGame> Table(PartyKingDbContextBase dbContext);

    /// <summary>
    /// Runs a write of the session's game, which loads it, changes it and saves it, while no other write of that
    /// session runs. Without it two writes that load the game before either saves both act on a copy without the
    /// other's change: the last two votes would each see a vote missing, and the round would never be counted. The
    /// time spent waiting for the lock is recorded under <paramref name="operation"/>.
    /// </summary>
    protected async Task<TResult> WriteAsync<TResult>(
        Guid sessionId, string operation, Func<Task<TResult>> write, CancellationToken cancellationToken)
    {
        var writeLock = _writeLocks[(uint)sessionId.GetHashCode() % (uint)_writeLocks.Length];
        await WaitForWriteLockAsync(operation, writeLock, cancellationToken);

        try
        {
            return await write();
        }
        finally
        {
            writeLock.Release();
        }
    }

    private async Task WaitForWriteLockAsync(
        string operation, SemaphoreSlim writeLock, CancellationToken cancellationToken)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var acquired = false;

        try
        {
            await writeLock.WaitAsync(cancellationToken);
            acquired = true;
        }
        finally
        {
            var waitMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            _metrics.RecordGameWriteLockWait(GetType().Name, operation, waitMs, acquired);
        }
    }

    protected async Task<WriterDbContext> CreateWriterAsync(CancellationToken cancellationToken)
    {
        return await _writerFactory.CreateDbContextAsync(cancellationToken);
    }

    protected Task<TGame?> LoadAsync(WriterDbContext dbContext, Guid sessionId, CancellationToken cancellationToken)
    {
        return Games(dbContext).FirstOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);
    }

    /// <summary>
    /// Concurrent reads of one session share a single query: a push makes every client fetch the game at once, and
    /// each query holds a thread while it waits for the database. A write drops the shared reads once it has
    /// committed, so a caller who starts asking after a write returned never gets a result that started before it.
    /// A caller that overlaps the commit itself may still join a read that began earlier. The callers only read
    /// the result.
    /// </summary>
    public async Task<TGame?> GetAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        // The read knows its own entry, so when it ends it removes that entry and never a newer read's
        Lazy<Task<TGame?>>? created = null;
        created = new Lazy<Task<TGame?>>(() => ReadSharedAsync(sessionId, created!));
        var read = _reads.GetOrAdd(sessionId, created);

        // The query is shared, so one caller's cancellation must not cancel it for the others
        return await read.Value.WaitAsync(cancellationToken);
    }

    private async Task<TGame?> ReadSharedAsync(Guid sessionId, Lazy<Task<TGame?>> self)
    {
        try
        {
            await using var dbContext = await _readerFactory.CreateDbContextAsync(CancellationToken.None);

            return await Games(dbContext)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.SessionId == sessionId, CancellationToken.None);
        }
        finally
        {
            _reads.TryRemove(new KeyValuePair<Guid, Lazy<Task<TGame?>>>(sessionId, self));
        }
    }

    /// <summary>Saves the changes and drops the shared reads, which may predate them.</summary>
    protected async Task SaveAsync(DbContext dbContext, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _reads.Clear();
        }
    }

    /// <summary>
    /// Saves what the write changed. When the save fails because the game was deleted meanwhile (a cascade delete of
    /// the session does not take the write lock), returns false; any other failure is thrown.
    /// </summary>
    private async Task<bool> TrySaveAsync(
        DbContext dbContext, Guid sessionId, CancellationToken cancellationToken)
    {
        try
        {
            await SaveAsync(dbContext, cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            if (await GameExistsAsync(sessionId, cancellationToken))
            {
                throw;
            }

            return false;
        }
    }

    private async Task<bool> GameExistsAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

        return await Table(dbContext).AnyAsync(x => x.SessionId == sessionId, cancellationToken);
    }

    public async Task<bool> TryCreateAsync(TGame game, CancellationToken cancellationToken)
    {
        return await WriteAsync(
            game.SessionId,
            nameof(TryCreateAsync),
            async () =>
            {
                await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

                if (await Table(dbContext).AnyAsync(x => x.SessionId == game.SessionId, cancellationToken))
                {
                    return false;
                }

                Table(dbContext).Add(game);

                try
                {
                    await SaveAsync(dbContext, cancellationToken);
                    return true;
                }
                catch (DbUpdateException ex) when (ex.IsUniqueViolation())
                {
                    // A start from outside the lock won the primary key
                    return false;
                }
            },
            cancellationToken);
    }

    public async Task<TGame?> OpenAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return await WriteAsync(
            sessionId,
            nameof(OpenAsync),
            async () =>
            {
                await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

                var game = await LoadAsync(dbContext, sessionId, cancellationToken);
                if (game is not null && game.Open())
                {
                    if (!await TrySaveAsync(dbContext, sessionId, cancellationToken))
                    {
                        return null;
                    }
                }

                return game;
            },
            cancellationToken);
    }

    public async Task<LobbyResult<TGame>> JoinAsync(
        Guid sessionId, TPlayer player, CancellationToken cancellationToken)
    {
        return await WriteAsync(
            sessionId,
            nameof(JoinAsync),
            async () =>
            {
                await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

                var game = await LoadAsync(dbContext, sessionId, cancellationToken);
                if (game is null)
                {
                    return new LobbyResult<TGame>(LobbyStatus.NoGame, null);
                }

                var status = game.Join(player);
                if (status != LobbyStatus.Done)
                {
                    return new LobbyResult<TGame>(status, game);
                }

                try
                {
                    await SaveAsync(dbContext, cancellationToken);
                }
                catch (DbUpdateException ex) when (ex.IsUniqueViolation())
                {
                    // A join from outside the lock with the same name won the unique index
                    return new LobbyResult<TGame>(LobbyStatus.NameTaken, game);
                }
                catch (DbUpdateException)
                {
                    if (await GameExistsAsync(sessionId, cancellationToken))
                    {
                        throw;
                    }

                    return new LobbyResult<TGame>(LobbyStatus.NoGame, null);
                }

                return new LobbyResult<TGame>(LobbyStatus.Done, game);
            },
            cancellationToken);
    }

    public async Task<LobbyResult<TGame>> KickAsync(
        Guid sessionId, Guid playerId, CancellationToken cancellationToken)
    {
        return await WriteAsync(
            sessionId,
            nameof(KickAsync),
            async () =>
            {
                await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

                var game = await LoadAsync(dbContext, sessionId, cancellationToken);
                if (game is null)
                {
                    return new LobbyResult<TGame>(LobbyStatus.NoGame, null);
                }

                var (status, kicked) = game.Kick(playerId);
                if (status != LobbyStatus.Done)
                {
                    return new LobbyResult<TGame>(status, game);
                }

                // The player left the game's list; the row has to go too
                PlayersOf(dbContext).Remove(kicked!);

                if (!await TrySaveAsync(dbContext, sessionId, cancellationToken))
                {
                    return new LobbyResult<TGame>(LobbyStatus.NoGame, null);
                }

                return new LobbyResult<TGame>(LobbyStatus.Done, game);
            },
            cancellationToken);
    }

    public async Task<LobbyResult<TGame>> ReorderAsync(
        Guid sessionId, IReadOnlyList<Guid> playerIds, CancellationToken cancellationToken)
    {
        return await WriteAsync(
            sessionId,
            nameof(ReorderAsync),
            async () =>
            {
                await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

                var game = await LoadAsync(dbContext, sessionId, cancellationToken);
                if (game is null)
                {
                    return new LobbyResult<TGame>(LobbyStatus.NoGame, null);
                }

                var status = game.Reorder(playerIds);
                if (status != LobbyStatus.Done)
                {
                    return new LobbyResult<TGame>(status, game);
                }

                if (!await TrySaveAsync(dbContext, sessionId, cancellationToken))
                {
                    return new LobbyResult<TGame>(LobbyStatus.NoGame, null);
                }

                return new LobbyResult<TGame>(LobbyStatus.Done, game);
            },
            cancellationToken);
    }

    public async Task<TResult> UpdateAsync<TResult>(
        Guid sessionId,
        string operation,
        TResult noGame,
        Func<TGame, TResult> action,
        CancellationToken cancellationToken)
        where TResult : IGameUpdateResult
    {
        return await WriteAsync(
            sessionId,
            operation,
            async () =>
            {
                await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

                var game = await LoadAsync(dbContext, sessionId, cancellationToken);
                if (game is null)
                {
                    return noGame;
                }

                var result = action(game);
                if (result.Changed && !await TrySaveAsync(dbContext, sessionId, cancellationToken))
                {
                    return noGame;
                }

                return result;
            },
            cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return await WriteAsync(
            sessionId,
            nameof(DeleteAsync),
            async () =>
            {
                await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

                var deleted = await Table(dbContext)
                    .Where(x => x.SessionId == sessionId)
                    .ExecuteDeleteAsync(cancellationToken);
                _reads.Clear();

                return deleted > 0;
            },
            cancellationToken);
    }
}
