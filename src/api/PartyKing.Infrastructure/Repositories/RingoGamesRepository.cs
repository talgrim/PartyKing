using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Games;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

public interface IRingoGamesRepository
{
    Task<RingoGame?> GetAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Creates the session's game; false when it already has one.</summary>
    Task<bool> TryCreateAsync(RingoGame game, CancellationToken cancellationToken);

    /// <summary>Appends the card to the flipped cards unless it is already there; null when the session has no game.</summary>
    Task<RingoGame?> FlipAsync(Guid sessionId, int cardId, CancellationToken cancellationToken);

    /// <summary>Deletes the session's game; false when there was none.</summary>
    Task<bool> DeleteAsync(Guid sessionId, CancellationToken cancellationToken);
}

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

    public async Task<RingoGame?> FlipAsync(Guid sessionId, int cardId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

        var game = await dbContext.RingoGames.FirstOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);
        if (game is null)
        {
            return null;
        }

        var flipped = game.FlippedCards.Length == 0
            ? []
            : game.FlippedCards.Split(',').Select(int.Parse).ToList();

        if (!flipped.Contains(cardId))
        {
            flipped.Add(cardId);
            game.FlippedCards = string.Join(',', flipped);
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
