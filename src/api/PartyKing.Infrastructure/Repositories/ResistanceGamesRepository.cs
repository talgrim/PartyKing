using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Results;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

internal class ResistanceGamesRepository
    : LobbyGamesRepository<ResistanceGame, ResistancePlayer>, IResistanceGamesRepository
{
    public ResistanceGamesRepository(
        IDbContextFactory<ReaderDbContext> readerFactory,
        IDbContextFactory<WriterDbContext> writerFactory)
        : base(readerFactory, writerFactory)
    {
    }

    protected override int MaxPlayers => ResistanceGame.MaxPlayers;

    protected override bool IsLocked(ResistanceGame game) => game.IsStarted;

    protected override IQueryable<ResistanceGame> Games(PartyKingDbContextBase dbContext)
    {
        // Split, so the three collections don't multiply into players x results x cards rows
        return dbContext.ResistanceGames
            .Include(x => x.Players)
            .Include(x => x.MissionResults)
            .Include(x => x.PlotCards)
            .AsSplitQuery();
    }

    protected override DbSet<ResistanceGame> Table(PartyKingDbContextBase dbContext) => dbContext.ResistanceGames;

    protected override DbSet<ResistancePlayer> PlayersOf(PartyKingDbContextBase dbContext) =>
        dbContext.ResistancePlayers;
}
