using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Games;
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

    public async Task<TGame?> GetAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _readerFactory.CreateDbContextAsync(cancellationToken);

        return await Games(dbContext)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);
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
            await dbContext.SaveChangesAsync(cancellationToken);
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
            await dbContext.SaveChangesAsync(cancellationToken);
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
            await dbContext.SaveChangesAsync(cancellationToken);
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

        await dbContext.SaveChangesAsync(cancellationToken);

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

        await dbContext.SaveChangesAsync(cancellationToken);

        return new LobbyResult<TGame>(LobbyStatus.Done, game);
    }

    public async Task<bool> DeleteAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

        var deleted = await Table(dbContext)
            .Where(x => x.SessionId == sessionId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted > 0;
    }
}
