using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Games;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

public interface IRingoGamesRepository
{
    Task<RingoGame?> GetAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Creates the session's game; false when it already has one.</summary>
    Task<bool> TryCreateAsync(RingoGame game, CancellationToken cancellationToken);

    /// <summary>Opens the game to guests (idempotent); null when the session has no game.</summary>
    Task<RingoGame?> OpenAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Records the flip of a card and makes it the pending one. A card that is already flipped changes nothing, and
    /// while another card is pending nothing is flipped (see <see cref="RingoFlipStatus"/>). With players, the flip is
    /// recorded under the name of the player whose turn it is, and <paramref name="turnOf"/> (null for a manager, who
    /// may flip out of turn) must be that player; without players <paramref name="flippedBy"/> is recorded.
    /// </summary>
    Task<RingoFlipResult> FlipAsync(
        Guid sessionId,
        int cardId,
        string flippedBy,
        Guid? turnOf,
        DateTimeOffset flippedAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Clears the pending card (idempotent) and, when there was one, passes the turn to the next player.
    /// <paramref name="turnOf"/> (null for a manager) must be the player whose turn it is.
    /// </summary>
    Task<RingoContinueResult> ContinueAsync(Guid sessionId, Guid? turnOf, CancellationToken cancellationToken);

    /// <summary>Adds the player at the end of the turn order, see <see cref="RingoPlayersStatus"/>.</summary>
    Task<RingoPlayersResult> JoinAsync(Guid sessionId, RingoPlayer player, CancellationToken cancellationToken);

    /// <summary>Removes a player; the turn moves on when it was theirs.</summary>
    Task<RingoPlayersResult> KickAsync(Guid sessionId, Guid playerId, CancellationToken cancellationToken);

    /// <summary>Sets the turn order to the given ids, which must be exactly the game's players.</summary>
    Task<RingoPlayersResult> ReorderAsync(
        Guid sessionId, IReadOnlyList<Guid> playerIds, CancellationToken cancellationToken);

    /// <summary>Deletes the session's game; false when there was none.</summary>
    Task<bool> DeleteAsync(Guid sessionId, CancellationToken cancellationToken);
}

public enum RingoFlipStatus
{
    NoGame,

    /// <summary>The card was flipped and is now the pending one.</summary>
    Flipped,

    /// <summary>The card was already flipped, nothing changed.</summary>
    AlreadyFlipped,

    /// <summary>The fourth king was flipped, so the game is over and nothing changed.</summary>
    GameOver,

    /// <summary>Another card is still pending, nothing changed.</summary>
    CardPending,

    /// <summary>The caller is a player but it is another player's turn, nothing changed.</summary>
    NotYourTurn,
}

public record RingoFlipResult(RingoFlipStatus Status, RingoGame? Game);

public enum RingoContinueStatus
{
    NoGame,
    Continued,
    NotYourTurn,
}

public record RingoContinueResult(RingoContinueStatus Status, RingoGame? Game);

public enum RingoPlayersStatus
{
    NoGame,
    NotOpen,
    NameTaken,
    NoPlayer,

    /// <summary>The ids to reorder are not exactly the game's players.</summary>
    OrderMismatch,
    Done,
}

public record RingoPlayersResult(RingoPlayersStatus Status, RingoGame? Game);

internal class RingoGamesRepository : IRingoGamesRepository
{
    private readonly IDbContextFactory<ReaderDbContext> _readerFactory;
    private readonly IDbContextFactory<WriterDbContext> _writerFactory;

    public RingoGamesRepository(
        IDbContextFactory<ReaderDbContext> readerFactory,
        IDbContextFactory<WriterDbContext> writerFactory)
    {
        _readerFactory = readerFactory;
        _writerFactory = writerFactory;
    }

    public async Task<RingoGame?> GetAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _readerFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.RingoGames
            .AsNoTracking()
            .Include(x => x.Flips)
            .Include(x => x.Players)
            .FirstOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);
    }

    public async Task<bool> TryCreateAsync(RingoGame game, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

        if (await dbContext.RingoGames.AnyAsync(x => x.SessionId == game.SessionId, cancellationToken))
        {
            return false;
        }

        dbContext.RingoGames.Add(game);

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

    public async Task<RingoGame?> OpenAsync(Guid sessionId, CancellationToken cancellationToken)
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

    public async Task<RingoFlipResult> FlipAsync(
        Guid sessionId,
        int cardId,
        string flippedBy,
        Guid? turnOf,
        DateTimeOffset flippedAt,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return new RingoFlipResult(RingoFlipStatus.NoGame, null);
        }

        var current = RingoTurns.Current(game);
        if (turnOf is not null && turnOf != current?.PlayerId)
        {
            return new RingoFlipResult(RingoFlipStatus.NotYourTurn, game);
        }

        if (game.Flips.Any(x => x.CardId == cardId))
        {
            return new RingoFlipResult(RingoFlipStatus.AlreadyFlipped, game);
        }

        if (game.IsOver)
        {
            return new RingoFlipResult(RingoFlipStatus.GameOver, game);
        }

        if (game.PendingCardId is not null)
        {
            return new RingoFlipResult(RingoFlipStatus.CardPending, game);
        }

        game.Flips.Add(new RingoFlip
        {
            SessionId = sessionId,
            Order = game.Flips.Count,
            CardId = cardId,
            FlippedAt = flippedAt,
            FlippedBy = current?.DisplayName ?? flippedBy,
        });
        game.PendingCardId = cardId;
        game.CurrentPlayerId = current?.PlayerId;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RingoFlipResult(RingoFlipStatus.Flipped, game);
    }

    public async Task<RingoContinueResult> ContinueAsync(
        Guid sessionId, Guid? turnOf, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return new RingoContinueResult(RingoContinueStatus.NoGame, null);
        }

        if (turnOf is not null && turnOf != RingoTurns.Current(game)?.PlayerId)
        {
            return new RingoContinueResult(RingoContinueStatus.NotYourTurn, game);
        }

        if (game.PendingCardId is not null)
        {
            game.PendingCardId = null;
            game.CurrentPlayerId = RingoTurns.Next(game)?.PlayerId;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new RingoContinueResult(RingoContinueStatus.Continued, game);
    }

    public async Task<RingoPlayersResult> JoinAsync(
        Guid sessionId, RingoPlayer player, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return new RingoPlayersResult(RingoPlayersStatus.NoGame, null);
        }

        if (!game.IsOpen)
        {
            return new RingoPlayersResult(RingoPlayersStatus.NotOpen, game);
        }

        if (game.Players.Any(x => x.NormalizedName == player.NormalizedName))
        {
            return new RingoPlayersResult(RingoPlayersStatus.NameTaken, game);
        }

        player.SessionId = sessionId;
        player.Order = game.Players.Count == 0 ? 0 : game.Players.Max(x => x.Order) + 1;
        game.Players.Add(player);
        game.CurrentPlayerId ??= player.PlayerId;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent join with the same name won the unique index
            return new RingoPlayersResult(RingoPlayersStatus.NameTaken, game);
        }

        return new RingoPlayersResult(RingoPlayersStatus.Done, game);
    }

    public async Task<RingoPlayersResult> KickAsync(Guid sessionId, Guid playerId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return new RingoPlayersResult(RingoPlayersStatus.NoGame, null);
        }

        var kicked = game.Players.FirstOrDefault(x => x.PlayerId == playerId);
        if (kicked is null)
        {
            return new RingoPlayersResult(RingoPlayersStatus.NoPlayer, game);
        }

        var wasCurrent = RingoTurns.Current(game)?.PlayerId == playerId;
        var next = RingoTurns.AfterRemoving(game, kicked, wasCurrent);

        dbContext.RingoPlayers.Remove(kicked);
        game.Players.Remove(kicked);
        var order = 0;
        foreach (var player in game.Players.OrderBy(x => x.Order))
        {
            player.Order = order++;
        }

        game.CurrentPlayerId = next?.PlayerId;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RingoPlayersResult(RingoPlayersStatus.Done, game);
    }

    public async Task<RingoPlayersResult> ReorderAsync(
        Guid sessionId, IReadOnlyList<Guid> playerIds, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return new RingoPlayersResult(RingoPlayersStatus.NoGame, null);
        }

        if (playerIds.Count != game.Players.Count
            || playerIds.Distinct().Count() != playerIds.Count
            || game.Players.Any(x => !playerIds.Contains(x.PlayerId)))
        {
            return new RingoPlayersResult(RingoPlayersStatus.OrderMismatch, game);
        }

        foreach (var player in game.Players)
        {
            player.Order = playerIds.ToList().IndexOf(player.PlayerId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RingoPlayersResult(RingoPlayersStatus.Done, game);
    }

    private static Task<RingoGame?> LoadAsync(
        WriterDbContext dbContext, Guid sessionId, CancellationToken cancellationToken)
    {
        return dbContext.RingoGames
            .Include(x => x.Flips)
            .Include(x => x.Players)
            .FirstOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

        var deleted = await dbContext.RingoGames
            .Where(x => x.SessionId == sessionId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted > 0;
    }
}
