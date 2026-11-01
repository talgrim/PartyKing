using PartyKing.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Games.Ringo;
using PartyKing.Infrastructure.Metrics;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

internal class RingoGamesRepository : LobbyGamesRepository<RingoGame, RingoPlayer>, IRingoGamesRepository
{
    public RingoGamesRepository(
        IDbContextFactory<ReaderDbContext> readerFactory,
        IDbContextFactory<WriterDbContext> writerFactory,
        RepositoryMetrics metrics)
        : base(readerFactory, writerFactory, metrics)
    {
    }

    protected override IQueryable<RingoGame> Games(PartyKingDbContextBase dbContext)
    {
        return dbContext.RingoGames.Include(x => x.Flips).Include(x => x.Players);
    }

    protected override DbSet<RingoGame> Table(PartyKingDbContextBase dbContext) => dbContext.RingoGames;

    protected override DbSet<RingoPlayer> PlayersOf(PartyKingDbContextBase dbContext) => dbContext.RingoPlayers;
}
