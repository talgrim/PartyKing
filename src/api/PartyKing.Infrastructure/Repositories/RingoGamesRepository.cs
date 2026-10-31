using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Results;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

internal class RingoGamesRepository : LobbyGamesRepository<RingoGame, RingoPlayer>, IRingoGamesRepository
{
    public RingoGamesRepository(
        IDbContextFactory<ReaderDbContext> readerFactory,
        IDbContextFactory<WriterDbContext> writerFactory)
        : base(readerFactory, writerFactory)
    {
    }

    protected override IQueryable<RingoGame> Games(PartyKingDbContextBase dbContext)
    {
        return dbContext.RingoGames.Include(x => x.Flips).Include(x => x.Players);
    }

    protected override DbSet<RingoGame> Table(PartyKingDbContextBase dbContext) => dbContext.RingoGames;

    protected override DbSet<RingoPlayer> PlayersOf(PartyKingDbContextBase dbContext) => dbContext.RingoPlayers;

    protected override void PlayerJoined(RingoGame game, RingoPlayer player)
    {
        game.CurrentPlayerId ??= player.PlayerId;
    }

    protected override void PlayerKicking(RingoGame game, RingoPlayer kicked)
    {
        var wasCurrent = RingoTurns.Current(game)?.PlayerId == kicked.PlayerId;
        game.CurrentPlayerId = RingoTurns.AfterRemoving(game, kicked, wasCurrent)?.PlayerId;
    }

    public async Task<RingoFlipResult> FlipAsync(
        Guid sessionId,
        int cardId,
        string flippedBy,
        Guid? turnOf,
        DateTimeOffset flippedAt,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

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
        await SaveAsync(dbContext, cancellationToken);

        return new RingoFlipResult(RingoFlipStatus.Flipped, game);
    }

    public async Task<RingoContinueResult> ContinueAsync(
        Guid sessionId, Guid? turnOf, CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

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
            await SaveAsync(dbContext, cancellationToken);
        }

        return new RingoContinueResult(RingoContinueStatus.Continued, game);
    }
}
