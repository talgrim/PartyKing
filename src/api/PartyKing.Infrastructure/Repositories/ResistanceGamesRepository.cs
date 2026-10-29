using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Games;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

public interface IResistanceGamesRepository : ILobbyGamesRepository<ResistanceGame, ResistancePlayer>
{
    /// <summary>
    /// Starts the open game: deals the roles and picks the first leader (see <see cref="ResistanceRoles"/>), which
    /// locks the players. Atomic with the players joining, so a join either makes it into the roles or is rejected.
    /// </summary>
    Task<ResistanceStartResult> StartAsync(Guid sessionId, DateTimeOffset startedAt, CancellationToken cancellationToken);

    /// <summary>
    /// The leader submits the team of the current mission, which locks it. Checked and saved in one writer context,
    /// so it can't race another submission.
    /// </summary>
    Task<ResistanceTeamResult> SelectTeamAsync(
        Guid sessionId, Guid? callerPlayerId, IReadOnlyList<Guid> playerIds, CancellationToken cancellationToken);
}

public enum ResistanceTeamStatus
{
    NoGame,
    NotStarted,

    /// <summary>The caller is not the player leading the mission.</summary>
    NotLeader,
    AlreadySubmitted,

    /// <summary>Not exactly <see cref="ResistanceRoles.TeamSize"/> distinct players.</summary>
    WrongSize,
    UnknownPlayer,
    Selected,
}

public record ResistanceTeamResult(ResistanceTeamStatus Status, ResistanceGame? Game);

public enum ResistanceStartStatus
{
    NoGame,
    NotOpen,
    AlreadyStarted,

    /// <summary>Fewer than <see cref="ResistanceGame.MinPlayers"/> players joined.</summary>
    NotEnoughPlayers,
    Started,
}

public record ResistanceStartResult(ResistanceStartStatus Status, ResistanceGame? Game);

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
        return dbContext.ResistanceGames.Include(x => x.Players);
    }

    protected override DbSet<ResistanceGame> Table(PartyKingDbContextBase dbContext) => dbContext.ResistanceGames;

    protected override DbSet<ResistancePlayer> PlayersOf(PartyKingDbContextBase dbContext) =>
        dbContext.ResistancePlayers;

    public async Task<ResistanceStartResult> StartAsync(
        Guid sessionId, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return new ResistanceStartResult(ResistanceStartStatus.NoGame, null);
        }

        if (game.IsStarted)
        {
            return new ResistanceStartResult(ResistanceStartStatus.AlreadyStarted, game);
        }

        if (!game.IsOpen)
        {
            return new ResistanceStartResult(ResistanceStartStatus.NotOpen, game);
        }

        if (game.Players.Count < ResistanceGame.MinPlayers)
        {
            return new ResistanceStartResult(ResistanceStartStatus.NotEnoughPlayers, game);
        }

        ResistanceRoles.Deal(game, Random.Shared);
        game.StartedAt = startedAt;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResistanceStartResult(ResistanceStartStatus.Started, game);
    }

    public async Task<ResistanceTeamResult> SelectTeamAsync(
        Guid sessionId, Guid? callerPlayerId, IReadOnlyList<Guid> playerIds, CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return new ResistanceTeamResult(ResistanceTeamStatus.NoGame, null);
        }

        if (!game.IsStarted)
        {
            return new ResistanceTeamResult(ResistanceTeamStatus.NotStarted, game);
        }

        if (callerPlayerId is null || callerPlayerId != game.LeaderPlayerId)
        {
            return new ResistanceTeamResult(ResistanceTeamStatus.NotLeader, game);
        }

        if (game.IsTeamSubmitted)
        {
            return new ResistanceTeamResult(ResistanceTeamStatus.AlreadySubmitted, game);
        }

        if (playerIds.Count != ResistanceRoles.TeamSize(game.Players.Count, game.MissionNumber)
            || playerIds.Distinct().Count() != playerIds.Count)
        {
            return new ResistanceTeamResult(ResistanceTeamStatus.WrongSize, game);
        }

        if (playerIds.Any(id => game.Players.All(x => x.PlayerId != id)))
        {
            return new ResistanceTeamResult(ResistanceTeamStatus.UnknownPlayer, game);
        }

        foreach (var player in game.Players)
        {
            player.IsOnTeam = playerIds.Contains(player.PlayerId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResistanceTeamResult(ResistanceTeamStatus.Selected, game);
    }
}
