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

    /// <summary>
    /// A player votes on the submitted team (a repeated vote replaces the earlier one). The vote that completes the
    /// round is tallied in the same save: a majority approves, otherwise (a tie too) the team is rejected, which
    /// counts towards the five that make the spies win.
    /// </summary>
    Task<ResistanceVoteResult> VoteAsync(
        Guid sessionId, Guid? callerPlayerId, bool approve, CancellationToken cancellationToken);

    /// <summary>
    /// Moves on after a rejected team: the next player leads, the team and the votes are cleared. The leader or a
    /// manager may do it.
    /// </summary>
    Task<ResistanceContinueResult> ContinueAsync(
        Guid sessionId, Guid? callerPlayerId, bool isManager, CancellationToken cancellationToken);
}

public enum ResistanceTeamStatus
{
    NoGame,
    NotStarted,
    GameOver,

    /// <summary>The caller is not the player leading the mission.</summary>
    NotLeader,
    AlreadySubmitted,

    /// <summary>Not exactly <see cref="ResistanceRoles.TeamSize"/> distinct players.</summary>
    WrongSize,
    UnknownPlayer,
    Selected,
}

public record ResistanceTeamResult(ResistanceTeamStatus Status, ResistanceGame? Game);

public enum ResistanceVoteStatus
{
    NoGame,
    NotStarted,
    GameOver,

    /// <summary>The caller did not join the game (a host or admin who only watches).</summary>
    NotPlayer,

    /// <summary>No team is up for a vote: it is not picked yet, or the votes are already in.</summary>
    NotVoting,
    Voted,
}

public record ResistanceVoteResult(ResistanceVoteStatus Status, ResistanceGame? Game);

public enum ResistanceContinueStatus
{
    NoGame,
    NotStarted,
    GameOver,

    /// <summary>Neither the leader nor a manager.</summary>
    NotAllowed,

    /// <summary>The votes are not in yet, or the team was approved (the mission itself is not built yet).</summary>
    NothingToContinue,
    Continued,
}

public record ResistanceContinueResult(ResistanceContinueStatus Status, ResistanceGame? Game);

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

        if (game.Phase == ResistancePhase.GameOver)
        {
            return new ResistanceTeamResult(ResistanceTeamStatus.GameOver, game);
        }

        if (callerPlayerId is null || callerPlayerId != game.LeaderPlayerId)
        {
            return new ResistanceTeamResult(ResistanceTeamStatus.NotLeader, game);
        }

        if (game.Phase != ResistancePhase.SelectingTeam)
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
            player.Vote = null;
        }

        game.Phase = ResistancePhase.Voting;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResistanceTeamResult(ResistanceTeamStatus.Selected, game);
    }

    public async Task<ResistanceVoteResult> VoteAsync(
        Guid sessionId, Guid? callerPlayerId, bool approve, CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return new ResistanceVoteResult(ResistanceVoteStatus.NoGame, null);
        }

        if (!game.IsStarted)
        {
            return new ResistanceVoteResult(ResistanceVoteStatus.NotStarted, game);
        }

        if (game.Phase == ResistancePhase.GameOver)
        {
            return new ResistanceVoteResult(ResistanceVoteStatus.GameOver, game);
        }

        var voter = game.Players.FirstOrDefault(x => x.PlayerId == callerPlayerId);
        if (voter is null)
        {
            return new ResistanceVoteResult(ResistanceVoteStatus.NotPlayer, game);
        }

        if (game.Phase != ResistancePhase.Voting)
        {
            return new ResistanceVoteResult(ResistanceVoteStatus.NotVoting, game);
        }

        voter.Vote = approve;
        if (game.Players.All(x => x.Vote is not null))
        {
            ResistanceRoles.ResolveVotes(game);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResistanceVoteResult(ResistanceVoteStatus.Voted, game);
    }

    public async Task<ResistanceContinueResult> ContinueAsync(
        Guid sessionId, Guid? callerPlayerId, bool isManager, CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return new ResistanceContinueResult(ResistanceContinueStatus.NoGame, null);
        }

        if (!game.IsStarted)
        {
            return new ResistanceContinueResult(ResistanceContinueStatus.NotStarted, game);
        }

        if (game.Phase == ResistancePhase.GameOver)
        {
            return new ResistanceContinueResult(ResistanceContinueStatus.GameOver, game);
        }

        if (!isManager && (callerPlayerId is null || callerPlayerId != game.LeaderPlayerId))
        {
            return new ResistanceContinueResult(ResistanceContinueStatus.NotAllowed, game);
        }

        if (game.Phase != ResistancePhase.VoteResult || ResistanceRoles.IsApproved(game))
        {
            return new ResistanceContinueResult(ResistanceContinueStatus.NothingToContinue, game);
        }

        ResistanceRoles.PassLeadership(game);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResistanceContinueResult(ResistanceContinueStatus.Continued, game);
    }
}
