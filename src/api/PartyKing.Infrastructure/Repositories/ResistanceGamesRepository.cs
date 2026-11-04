using PartyKing.Application.Events;
using PartyKing.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Games.Base;
using PartyKing.Domain.Entities.Games.Resistance;
using PartyKing.Infrastructure.Metrics;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

internal class ResistanceGamesRepository
    : LobbyGamesRepository<ResistanceGame, ResistancePlayer>, IResistanceGamesRepository
{
    public ResistanceGamesRepository(
        IDbContextFactory<ReaderDbContext> readerFactory,
        IDbContextFactory<WriterDbContext> writerFactory,
        RepositoryMetrics metrics,
        IDomainEventDispatcher dispatcher)
        : base(readerFactory, writerFactory, metrics, dispatcher)
    {
    }

    protected override IQueryable<ResistanceGame> Games(PartyKingDbContextBase dbContext)
    {
        // Split, so the three collections don't multiply into players x results x cards rows
        return dbContext.ResistanceGames
            .Include(x => x.Players)
            .Include(x => x.MissionResults)
            .Include(x => x.PlotCards)
            .AsSplitQuery();
    }

    protected override GameKind Kind => GameKind.Resistance;

    /// <summary>
    /// One projection, no game graph and no shared read: the lobby is fetched by every tab on its own pushes and
    /// needs four fields. <c>StartedAt != null</c> is <c>IsStarted</c> (the property itself is not mapped).
    /// </summary>
    public async Task<ResistanceLobbyState?> GetLobbyAsync(
        Guid sessionId, Guid? playerId, CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateReaderAsync(cancellationToken);

        return await dbContext.ResistanceGames
            .AsNoTracking()
            .Where(x => x.SessionId == sessionId)
            .Select(x => new ResistanceLobbyState(
                x.CreatedAt,
                x.IsOpen,
                x.StartedAt != null,
                playerId != null && x.Players.Any(p => p.PlayerId == playerId)))
            .FirstOrDefaultAsync(cancellationToken);
    }

    protected override DbSet<ResistanceGame> Table(PartyKingDbContextBase dbContext) => dbContext.ResistanceGames;

    protected override DbSet<ResistancePlayer> PlayersOf(PartyKingDbContextBase dbContext) =>
        dbContext.ResistancePlayers;
}
