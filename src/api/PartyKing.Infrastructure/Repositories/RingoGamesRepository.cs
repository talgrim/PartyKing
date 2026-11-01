using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Games;
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
}
