using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Results;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

/// <summary>
/// The EF Core part every lobby game shares: reading, creating, opening and deleting the game and the
/// join/kick/reorder of its players. A game adds its own state through the hooks.
/// </summary>
internal abstract class LobbyGamesRepository<TGame, TPlayer> : ILobbyGamesRepository<TGame, TPlayer>
    where TGame : LobbyGame<TPlayer>
    where TPlayer : LobbyPlayer
{
    private readonly IDbContextFactory<ReaderDbContext> _readerFactory;
    private readonly IDbContextFactory<WriterDbContext> _writerFactory;

    // The read in flight per session, shared by every caller that asks for it meanwhile
    private readonly ConcurrentDictionary<Guid, Lazy<Task<TGame?>>> _reads = new();

    protected LobbyGamesRepository(
        IDbContextFactory<ReaderDbContext> readerFactory,
        IDbContextFactory<WriterDbContext> writerFactory)
    {
        _readerFactory = readerFactory;
        _writerFactory = writerFactory;
    }

    /// <summary>How many players may join.</summary>
    protected virtual int MaxPlayers => int.MaxValue;

    /// <summary>Whether the game's players are locked, so nobody joins, leaves or moves.</summary>
    protected virtual bool IsLocked(TGame game) => false;

    /// <summary>The game's table with everything the game needs loaded, including its players.</summary>
    protected abstract IQueryable<TGame> Games(PartyKingDbContextBase dbContext);

    protected abstract DbSet<TPlayer> PlayersOf(PartyKingDbContextBase dbContext);

    protected abstract DbSet<TGame> Table(PartyKingDbContextBase dbContext);

    /// <summary>Called after the player was added to the game, before it is saved.</summary>
    protected virtual void PlayerJoined(TGame game, TPlayer player)
    {
    }

    /// <summary>Called while the player is still in the game, so a game can work out what follows their leaving.</summary>
    protected virtual void PlayerKicking(TGame game, TPlayer kicked)
    {
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
    /// each query holds a thread while it waits for the database. A write drops the shared read, so a caller who
    /// asks after a write finished never gets a result that started before it. The callers only read the result.
    /// </summary>
    public async Task<TGame?> GetAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var read = _reads.GetOrAdd(sessionId, id => new Lazy<Task<TGame?>>(() => ReadSharedAsync(id)));

        // The query is shared, so one caller's cancellation must not cancel it for the others
        return await read.Value.WaitAsync(cancellationToken);
    }

    private async Task<TGame?> ReadSharedAsync(Guid sessionId)
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
            _reads.TryRemove(sessionId, out _);
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

    public async Task<bool> TryCreateAsync(TGame game, CancellationToken cancellationToken)
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
        catch (DbUpdateException)
        {
            // A concurrent start won the primary key
            return false;
        }
    }

    public async Task<TGame?> OpenAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is not null && !game.IsOpen)
        {
            game.IsOpen = true;
            await SaveAsync(dbContext, cancellationToken);
        }

        return game;
    }

    public async Task<LobbyResult<TGame>> JoinAsync(
        Guid sessionId, TPlayer player, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return new LobbyResult<TGame>(LobbyStatus.NoGame, null);
        }

        if (!game.IsOpen)
        {
            return new LobbyResult<TGame>(LobbyStatus.NotOpen, game);
        }

        if (IsLocked(game))
        {
            return new LobbyResult<TGame>(LobbyStatus.Started, game);
        }

        if (game.Players.Any(x => x.NormalizedName == player.NormalizedName))
        {
            return new LobbyResult<TGame>(LobbyStatus.NameTaken, game);
        }

        if (game.Players.Count >= MaxPlayers)
        {
            return new LobbyResult<TGame>(LobbyStatus.Full, game);
        }

        player.SessionId = sessionId;
        player.Order = game.Players.Count == 0 ? 0 : game.Players.Max(x => x.Order) + 1;
        game.Players.Add(player);
        PlayerJoined(game, player);

        try
        {
            await SaveAsync(dbContext, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent join with the same name won the unique index
            return new LobbyResult<TGame>(LobbyStatus.NameTaken, game);
        }

        return new LobbyResult<TGame>(LobbyStatus.Done, game);
    }

    public async Task<LobbyResult<TGame>> KickAsync(
        Guid sessionId, Guid playerId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return new LobbyResult<TGame>(LobbyStatus.NoGame, null);
        }

        if (IsLocked(game))
        {
            return new LobbyResult<TGame>(LobbyStatus.Started, game);
        }

        var kicked = game.Players.FirstOrDefault(x => x.PlayerId == playerId);
        if (kicked is null)
        {
            return new LobbyResult<TGame>(LobbyStatus.NoPlayer, game);
        }

        PlayerKicking(game, kicked);

        PlayersOf(dbContext).Remove(kicked);
        game.Players.Remove(kicked);
        var order = 0;
        foreach (var player in game.Players.OrderBy(x => x.Order))
        {
            player.Order = order++;
        }

        await SaveAsync(dbContext, cancellationToken);

        return new LobbyResult<TGame>(LobbyStatus.Done, game);
    }

    public async Task<LobbyResult<TGame>> ReorderAsync(
        Guid sessionId, IReadOnlyList<Guid> playerIds, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return new LobbyResult<TGame>(LobbyStatus.NoGame, null);
        }

        if (IsLocked(game))
        {
            return new LobbyResult<TGame>(LobbyStatus.Started, game);
        }

        if (playerIds.Count != game.Players.Count
            || playerIds.Distinct().Count() != playerIds.Count
            || game.Players.Any(x => !playerIds.Contains(x.PlayerId)))
        {
            return new LobbyResult<TGame>(LobbyStatus.OrderMismatch, game);
        }

        foreach (var player in game.Players)
        {
            player.Order = playerIds.ToList().IndexOf(player.PlayerId);
        }

        await SaveAsync(dbContext, cancellationToken);

        return new LobbyResult<TGame>(LobbyStatus.Done, game);
    }

    public async Task<bool> DeleteAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

        var deleted = await Table(dbContext)
            .Where(x => x.SessionId == sessionId)
            .ExecuteDeleteAsync(cancellationToken);
        _reads.Clear();

        return deleted > 0;
    }
}
