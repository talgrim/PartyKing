using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Games;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

public interface IRingoGamesRepository
{
    Task<RingoGame?> GetAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Creates the session's game; false when it already has one.</summary>
    Task<bool> TryCreateAsync(RingoGame game, CancellationToken cancellationToken);

    /// <summary>
    /// Records the flip of a card and makes it the pending one. A card that is already flipped changes nothing, and
    /// while another card is pending nothing is flipped (see <see cref="RingoFlipStatus"/>).
    /// </summary>
    Task<RingoFlipResult> FlipAsync(
        Guid sessionId, int cardId, string flippedBy, DateTimeOffset flippedAt, CancellationToken cancellationToken);

    /// <summary>Clears the pending card (idempotent); null when the session has no game.</summary>
    Task<RingoGame?> ContinueAsync(Guid sessionId, CancellationToken cancellationToken);

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
}

public record RingoFlipResult(RingoFlipStatus Status, RingoGame? Game);

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

    public async Task<RingoFlipResult> FlipAsync(
        Guid sessionId, int cardId, string flippedBy, DateTimeOffset flippedAt, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

        var game = await dbContext.RingoGames
            .Include(x => x.Flips)
            .FirstOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);
        if (game is null)
        {
            return new RingoFlipResult(RingoFlipStatus.NoGame, null);
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
            FlippedBy = flippedBy,
        });
        game.PendingCardId = cardId;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RingoFlipResult(RingoFlipStatus.Flipped, game);
    }

    public async Task<RingoGame?> ContinueAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

        var game = await dbContext.RingoGames
            .Include(x => x.Flips)
            .FirstOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);
        if (game is not null && game.PendingCardId is not null)
        {
            game.PendingCardId = null;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return game;
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
