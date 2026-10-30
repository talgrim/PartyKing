using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Games;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

public interface IResistanceGamesRepository : ILobbyGamesRepository<ResistanceGame, ResistancePlayer>
{
    /// <summary>
    /// Replaces the game's rules with the given ones. Only while the game is still closed and not started, so the
    /// players never see the numbers change under them.
    /// </summary>
    Task<ResistanceRulesResult> RefreshRulesAsync(
        Guid sessionId, ResistanceRules rules, CancellationToken cancellationToken);

    /// <summary>
    /// Switches the plot cards add-on on or off in the game's rules. Only while the game is still closed and not
    /// started, like <see cref="RefreshRulesAsync"/>.
    /// </summary>
    Task<ResistanceRulesResult> SetPlotCardsAsync(Guid sessionId, bool enabled, CancellationToken cancellationToken);

    /// <summary>
    /// Starts the open game: deals the roles and picks the first leader (see <see cref="ResistanceRoles"/>), which
    /// locks the players. Atomic with the players joining, so a join either makes it into the roles or is rejected.
    /// </summary>
    Task<ResistanceStartResult> StartAsync(Guid sessionId, DateTimeOffset startedAt, CancellationToken cancellationToken);

    /// <summary>Starts a finished game over with the same players: new roles and leader, mission 1, no results.</summary>
    Task<ResistanceRestartResult> RestartAsync(Guid sessionId, DateTimeOffset startedAt, CancellationToken cancellationToken);

    /// <summary>
    /// The leader submits the team of the current mission, which locks it. Checked and saved in one writer context,
    /// so it can't race another submission.
    /// </summary>
    Task<ResistanceTeamResult> SelectTeamAsync(
        Guid sessionId, Guid? callerPlayerId, IReadOnlyList<Guid> playerIds, CancellationToken cancellationToken);

    /// <summary>
    /// The leader hands the cards drawn for this mission to the other players (every card but Establish Confidence,
    /// which the leader keeps), which moves the game on to picking the team. Checked and saved in one writer context.
    /// </summary>
    Task<ResistanceDealResult> DealPlotCardsAsync(
        Guid sessionId, Guid? callerPlayerId, IReadOnlyList<PlotCardAssignment> assignments,
        CancellationToken cancellationToken);

    /// <summary>
    /// The holder of a card plays it on a player. An immediate card (Overheard Conversation, Establish Confidence, Open
    /// Up) is played while the immediate cards are resolved, on a neighbour for Overheard Conversation and any other
    /// player for the rest. In the Spotlight is played while the mission is played, on a team member who has not
    /// played their mission card yet, and makes that card public.
    /// </summary>
    Task<ResistancePlayCardResult> PlayPlotCardAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, Guid targetPlayerId, CancellationToken cancellationToken);

    /// <summary>
    /// The holder of Take Responsibility takes a plot card from another player, between the votes (not while a team is
    /// voted on, which would change who votes first). With a face-up Opinion Maker and cards in hand the caller says
    /// which to take (<paramref name="takeOpinionMaker"/>); a hand card is picked at random. Nothing to take is lost.
    /// </summary>
    Task<ResistancePlayCardResult> TakeResponsibilityAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, Guid targetPlayerId, bool? takeOpinionMaker,
        CancellationToken cancellationToken);

    /// <summary>
    /// In the leader window the holder of Strong Leader takes the lead, which ends the window: the new leader draws
    /// their cards or picks the team.
    /// </summary>
    Task<ResistancePlayCardResult> TakeLeadAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, CancellationToken cancellationToken);

    /// <summary>
    /// A player confirms in the leader window that they do not play a card (repeating it is fine). The last
    /// confirmation ends the window.
    /// </summary>
    Task<ResistancePlayCardResult> PassLeaderWindowAsync(
        Guid sessionId, Guid? callerPlayerId, CancellationToken cancellationToken);

    /// <summary>
    /// The player who saw the identity card confirms it. The last confirmation of the round moves on to picking the team.
    /// </summary>
    Task<ResistancePlayCardResult> ConfirmPlotCardAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, CancellationToken cancellationToken);

    /// <summary>
    /// A player votes on the submitted team (a repeated vote replaces the earlier one). The vote that completes the
    /// round is tallied in the same save: a majority approves, otherwise (a tie too) the team is rejected, which
    /// counts towards the five that make the spies win.
    /// </summary>
    Task<ResistanceVoteResult> VoteAsync(
        Guid sessionId, Guid? callerPlayerId, bool approve, CancellationToken cancellationToken);

    /// <summary>
    /// A member of the approved team secretly plays a Success or Fail card; it can't be changed. The card that
    /// completes the team is resolved in the same save: the mission fails when the Fail cards reach the rules'
    /// requirement, and the third success or failure ends the game.
    /// </summary>
    Task<ResistanceMissionCardResult> PlayMissionCardAsync(
        Guid sessionId, Guid? callerPlayerId, bool success, CancellationToken cancellationToken);

    /// <summary>
    /// Moves on after a rejected team or a finished mission: the next player leads, the team, the votes and the cards
    /// are cleared (a finished mission also starts the next one). The leader or a manager may do it.
    /// </summary>
    Task<ResistanceContinueResult> ContinueAsync(
        Guid sessionId, Guid? callerPlayerId, bool isManager, CancellationToken cancellationToken);
}

public enum ResistanceRulesStatus
{
    NoGame,

    /// <summary>The game is already open to guests or started.</summary>
    NotEditable,
    Refreshed,
}

public record ResistanceRulesResult(ResistanceRulesStatus Status, ResistanceGame? Game);

public enum ResistanceTeamStatus
{
    NoGame,
    NotStarted,
    GameOver,

    /// <summary>The caller is not the player leading the mission.</summary>
    NotLeader,
    AlreadySubmitted,

    /// <summary>The leader still has to hand out the plot cards.</summary>
    CardsNotDealt,

    /// <summary>Not exactly <see cref="ResistanceRules.TeamSize"/> distinct players.</summary>
    WrongSize,
    UnknownPlayer,
    Selected,
}

public record ResistanceTeamResult(ResistanceTeamStatus Status, ResistanceGame? Game);

/// <summary>The leader gives the card with this id to the player.</summary>
public record PlotCardAssignment(int CardId, Guid PlayerId);

public enum ResistanceDealStatus
{
    NoGame,
    NotStarted,
    GameOver,

    /// <summary>The caller is not the player leading the mission.</summary>
    NotLeader,

    /// <summary>No cards are waiting to be handed out (add-on off, or already dealt).</summary>
    NotDealing,

    /// <summary>A card that was not drawn, is kept by the leader, or is given twice.</summary>
    UnknownCard,

    /// <summary>A receiver that is not a player, or the leader themselves.</summary>
    InvalidReceiver,

    /// <summary>Some of the cards to hand out are left.</summary>
    Incomplete,
    Dealt,
}

public record ResistanceDealResult(ResistanceDealStatus Status, ResistanceGame? Game);

public enum ResistancePlayCardStatus
{
    NoGame,
    NotStarted,
    GameOver,

    /// <summary>The immediate cards are not being played.</summary>
    NotResolving,

    /// <summary>In the Spotlight is played while the mission cards are played, and the mission is not.</summary>
    NotPlayingMission,

    /// <summary>No such card, or it is not an immediate card.</summary>
    UnknownCard,

    /// <summary>The caller does not hold the card (to play it), or is not the one it shows the identity card to (to confirm it).</summary>
    NotAllowed,

    /// <summary>The card was played already (to play it), or has not been played yet (to confirm it).</summary>
    WrongState,

    /// <summary>The target is not a legal one for the card.</summary>
    InvalidTarget,

    /// <summary>The card can't be played in this step of the round.</summary>
    WrongPhase,

    /// <summary>The caller did not join the game (a host or admin who only watches).</summary>
    NotPlayer,

    /// <summary>The target has a face-up Opinion Maker and cards in hand, and the caller did not say which to take.</summary>
    ChoiceRequired,
    Done,
}

public record ResistancePlayCardResult(ResistancePlayCardStatus Status, ResistanceGame? Game);

public enum ResistanceVoteStatus
{
    NoGame,
    NotStarted,
    GameOver,

    /// <summary>The caller did not join the game (a host or admin who only watches).</summary>
    NotPlayer,

    /// <summary>No team is up for a vote: it is not picked yet, or the votes are already in.</summary>
    NotVoting,

    /// <summary>The caller is an Opinion Maker and already revealed their vote, which can't be changed.</summary>
    VoteLocked,

    /// <summary>An Opinion Maker in play has not revealed their vote yet, so the others wait.</summary>
    OpinionMakersFirst,
    Voted,
}

public record ResistanceVoteResult(ResistanceVoteStatus Status, ResistanceGame? Game);

public enum ResistanceMissionStatus
{
    NoGame,
    NotStarted,
    GameOver,

    /// <summary>The caller did not join the game (a host or admin who only watches).</summary>
    NotPlayer,

    /// <summary>No approved team is playing a mission.</summary>
    NotPlaying,

    /// <summary>The caller is not on the team.</summary>
    NotOnTeam,
    AlreadyPlayed,

    /// <summary>A member of the resistance may only play Success.</summary>
    MustSucceed,
    Played,
}

public record ResistanceMissionCardResult(ResistanceMissionStatus Status, ResistanceGame? Game);

public enum ResistanceContinueStatus
{
    NoGame,
    NotStarted,
    GameOver,

    /// <summary>Neither the leader nor a manager.</summary>
    NotAllowed,

    /// <summary>The votes are not in yet, or the approved team's mission is still being played.</summary>
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

public enum ResistanceRestartStatus
{
    NoGame,
    NotStarted,

    /// <summary>The game is still being played; only a finished one restarts.</summary>
    NotOver,
    Restarted,
}

public record ResistanceRestartResult(ResistanceRestartStatus Status, ResistanceGame? Game);

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
        return dbContext.ResistanceGames.Include(x => x.Players).Include(x => x.MissionResults).Include(x => x.PlotCards);
    }

    protected override DbSet<ResistanceGame> Table(PartyKingDbContextBase dbContext) => dbContext.ResistanceGames;

    protected override DbSet<ResistancePlayer> PlayersOf(PartyKingDbContextBase dbContext) =>
        dbContext.ResistancePlayers;

    public async Task<ResistanceRulesResult> RefreshRulesAsync(
        Guid sessionId, ResistanceRules rules, CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return new ResistanceRulesResult(ResistanceRulesStatus.NoGame, null);
        }

        if (game.IsOpen || game.IsStarted)
        {
            return new ResistanceRulesResult(ResistanceRulesStatus.NotEditable, game);
        }

        rules.PlotCardsEnabled = game.Rules.PlotCardsEnabled;
        game.Rules = rules;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResistanceRulesResult(ResistanceRulesStatus.Refreshed, game);
    }

    public async Task<ResistanceRulesResult> SetPlotCardsAsync(
        Guid sessionId, bool enabled, CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return new ResistanceRulesResult(ResistanceRulesStatus.NoGame, null);
        }

        if (game.IsOpen || game.IsStarted)
        {
            return new ResistanceRulesResult(ResistanceRulesStatus.NotEditable, game);
        }

        // Rules is a JSON column compared by reference, so assign a copy to have the change detected.
        game.Rules = new ResistanceRules
        {
            PlayerCounts = game.Rules.PlayerCounts,
            PlotDeck = game.Rules.PlotDeck,
            PlotCardsEnabled = enabled,
        };
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResistanceRulesResult(ResistanceRulesStatus.Refreshed, game);
    }

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

    public async Task<ResistanceRestartResult> RestartAsync(
        Guid sessionId, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return new ResistanceRestartResult(ResistanceRestartStatus.NoGame, null);
        }

        if (!game.IsStarted)
        {
            return new ResistanceRestartResult(ResistanceRestartStatus.NotStarted, game);
        }

        if (game.Phase != ResistancePhase.GameOver)
        {
            return new ResistanceRestartResult(ResistanceRestartStatus.NotOver, game);
        }

        ResistanceRoles.Reset(game);
        ResistanceRoles.Deal(game, Random.Shared);
        game.StartedAt = startedAt;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResistanceRestartResult(ResistanceRestartStatus.Restarted, game);
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

        if (game.Phase is ResistancePhase.DealingCards or ResistancePhase.ResolvingCards)
        {
            return new ResistanceTeamResult(ResistanceTeamStatus.CardsNotDealt, game);
        }

        if (game.Phase != ResistancePhase.SelectingTeam)
        {
            return new ResistanceTeamResult(ResistanceTeamStatus.AlreadySubmitted, game);
        }

        if (playerIds.Count != game.Rules.TeamSize(game.Players.Count, game.MissionNumber)
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
            player.MissionCard = null;
        }

        game.Phase = ResistancePhase.Voting;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResistanceTeamResult(ResistanceTeamStatus.Selected, game);
    }

    public async Task<ResistanceDealResult> DealPlotCardsAsync(
        Guid sessionId, Guid? callerPlayerId, IReadOnlyList<PlotCardAssignment> assignments,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return new ResistanceDealResult(ResistanceDealStatus.NoGame, null);
        }

        if (!game.IsStarted)
        {
            return new ResistanceDealResult(ResistanceDealStatus.NotStarted, game);
        }

        if (game.Phase == ResistancePhase.GameOver)
        {
            return new ResistanceDealResult(ResistanceDealStatus.GameOver, game);
        }

        if (callerPlayerId is null || callerPlayerId != game.LeaderPlayerId)
        {
            return new ResistanceDealResult(ResistanceDealStatus.NotLeader, game);
        }

        if (game.Phase != ResistancePhase.DealingCards)
        {
            return new ResistanceDealResult(ResistanceDealStatus.NotDealing, game);
        }

        var toDeal = ResistanceRoles.CardsToDeal(game).ToDictionary(x => x.CardId);
        if (assignments.Any(x => !toDeal.ContainsKey(x.CardId))
            || assignments.Select(x => x.CardId).Distinct().Count() != assignments.Count)
        {
            return new ResistanceDealResult(ResistanceDealStatus.UnknownCard, game);
        }

        if (assignments.Any(x => x.PlayerId == game.LeaderPlayerId || game.Players.All(p => p.PlayerId != x.PlayerId)))
        {
            return new ResistanceDealResult(ResistanceDealStatus.InvalidReceiver, game);
        }

        if (assignments.Count != toDeal.Count)
        {
            return new ResistanceDealResult(ResistanceDealStatus.Incomplete, game);
        }

        foreach (var assignment in assignments)
        {
            var card = toDeal[assignment.CardId];
            // An Opinion Maker is played the moment it is dealt and stays face up
            card.Location = card.Type == ResistancePlotCardType.OpinionMaker
                ? ResistancePlotCardLocation.InPlay
                : ResistancePlotCardLocation.Hand;
            card.HolderPlayerId = assignment.PlayerId;
        }

        // The leader keeps the cards they play on themselves (Establish Confidence)
        foreach (var kept in game.PlotCards.Where(x => x.Location == ResistancePlotCardLocation.Drawn))
        {
            kept.Location = ResistancePlotCardLocation.Hand;
        }

        ResistanceRoles.AfterDeal(game);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResistanceDealResult(ResistanceDealStatus.Dealt, game);
    }

    public async Task<ResistancePlayCardResult> PlayPlotCardAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, Guid targetPlayerId, CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.NoGame, null);
        }

        if (!game.IsStarted)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.NotStarted, game);
        }

        if (game.Phase == ResistancePhase.GameOver)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.GameOver, game);
        }

        var card = game.PlotCards.Find(x => x.CardId == cardId);
        var spotlight = card?.Type == ResistancePlotCardType.InTheSpotlight;
        if (spotlight && game.Phase != ResistancePhase.PlayingMission)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.NotPlayingMission, game);
        }

        if (!spotlight && game.Phase != ResistancePhase.ResolvingCards)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.NotResolving, game);
        }

        if (card is null || !(spotlight || ResistanceRoles.IsImmediate(card.Type))
            || card.Location is not (ResistancePlotCardLocation.Hand or ResistancePlotCardLocation.Played))
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.UnknownCard, game);
        }

        if (callerPlayerId is null || card.HolderPlayerId != callerPlayerId)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.NotAllowed, game);
        }

        if (card.Location == ResistancePlotCardLocation.Played)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.WrongState, game);
        }

        if (spotlight && ResistanceRoles.OpenMissionPlayerId(game) is not null)
        {
            // One player is forced open per mission
            return new ResistancePlayCardResult(ResistancePlayCardStatus.WrongState, game);
        }

        var validTarget = card.Type switch
        {
            ResistancePlotCardType.OverheardConversation =>
                ResistanceRoles.Neighbours(game, card.HolderPlayerId!.Value).Contains(targetPlayerId),
            ResistancePlotCardType.InTheSpotlight =>
                ResistanceRoles.CanBeForcedOpen(game, card.HolderPlayerId!.Value, targetPlayerId),
            _ => targetPlayerId != card.HolderPlayerId && game.Players.Any(x => x.PlayerId == targetPlayerId),
        };
        if (!validTarget)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.InvalidTarget, game);
        }

        card.Location = ResistancePlotCardLocation.Played;
        card.TargetPlayerId = targetPlayerId;
        card.PlayedAt = DateTimeOffset.UtcNow;
        card.PlayedInMission = game.MissionNumber;
        card.IsConfirmed = spotlight;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResistancePlayCardResult(ResistancePlayCardStatus.Done, game);
    }

    public async Task<ResistancePlayCardResult> TakeResponsibilityAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, Guid targetPlayerId, bool? takeOpinionMaker,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.NoGame, null);
        }

        if (!game.IsStarted)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.NotStarted, game);
        }

        if (game.Phase == ResistancePhase.GameOver)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.GameOver, game);
        }

        var card = game.PlotCards.Find(x => x.CardId == cardId);
        if (card is null || card.Type != ResistancePlotCardType.TakeResponsibility
            || card.Location is not (ResistancePlotCardLocation.Hand or ResistancePlotCardLocation.Played))
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.UnknownCard, game);
        }

        if (callerPlayerId is null || card.HolderPlayerId != callerPlayerId)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.NotAllowed, game);
        }

        if (card.Location == ResistancePlotCardLocation.Played)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.WrongState, game);
        }

        if (game.Phase is not (ResistancePhase.SelectingTeam or ResistancePhase.PlayingMission or ResistancePhase.MissionResult))
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.WrongPhase, game);
        }

        if (targetPlayerId == callerPlayerId || game.Players.All(x => x.PlayerId != targetPlayerId))
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.InvalidTarget, game);
        }

        var opinionMaker = game.PlotCards.Find(x => x.HolderPlayerId == targetPlayerId
                                                    && x.Location == ResistancePlotCardLocation.InPlay
                                                    && x.Type == ResistancePlotCardType.OpinionMaker);
        var hand = game.PlotCards
            .Where(x => x.HolderPlayerId == targetPlayerId && x.Location == ResistancePlotCardLocation.Hand)
            .ToList();
        if (opinionMaker is not null && hand.Count > 0 && takeOpinionMaker is null)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.ChoiceRequired, game);
        }

        var taken = opinionMaker is not null && (hand.Count == 0 || takeOpinionMaker == true)
            ? opinionMaker
            : hand.Count > 0 ? hand[Random.Shared.Next(hand.Count)] : null;
        if (taken is not null)
        {
            taken.HolderPlayerId = callerPlayerId;
        }

        card.Location = ResistancePlotCardLocation.Played;
        card.TargetPlayerId = targetPlayerId;
        card.PlayedAt = DateTimeOffset.UtcNow;
        card.PlayedInMission = game.MissionNumber;
        card.IsConfirmed = true;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResistancePlayCardResult(ResistancePlayCardStatus.Done, game);
    }

    public async Task<ResistancePlayCardResult> TakeLeadAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

        var (early, game) = await LoadStartedAsync(dbContext, sessionId, cancellationToken);
        if (early is { } status)
        {
            return new ResistancePlayCardResult(status, game);
        }

        var card = game!.PlotCards.Find(x => x.CardId == cardId);
        if (card is null || card.Type != ResistancePlotCardType.StrongLeader
            || card.Location is not (ResistancePlotCardLocation.Hand or ResistancePlotCardLocation.Played))
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.UnknownCard, game);
        }

        if (callerPlayerId is null || card.HolderPlayerId != callerPlayerId)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.NotAllowed, game);
        }

        if (card.Location == ResistancePlotCardLocation.Played)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.WrongState, game);
        }

        if (game.Phase != ResistancePhase.LeaderWindow)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.WrongPhase, game);
        }

        if (callerPlayerId == game.LeaderPlayerId)
        {
            // Taking the lead from yourself changes nothing
            return new ResistancePlayCardResult(ResistancePlayCardStatus.InvalidTarget, game);
        }

        card.Location = ResistancePlotCardLocation.Played;
        card.TargetPlayerId = game.LeaderPlayerId;
        card.PlayedAt = DateTimeOffset.UtcNow;
        card.PlayedInMission = game.MissionNumber;
        card.IsConfirmed = true;
        game.LeaderPlayerId = callerPlayerId;
        ResistanceRoles.AfterLeaderWindow(game);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResistancePlayCardResult(ResistancePlayCardStatus.Done, game);
    }

    public async Task<ResistancePlayCardResult> PassLeaderWindowAsync(
        Guid sessionId, Guid? callerPlayerId, CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

        var (early, game) = await LoadStartedAsync(dbContext, sessionId, cancellationToken);
        if (early is { } status)
        {
            return new ResistancePlayCardResult(status, game);
        }

        var player = game!.Players.Find(x => x.PlayerId == callerPlayerId);
        if (player is null)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.NotPlayer, game);
        }

        if (game.Phase != ResistancePhase.LeaderWindow)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.WrongPhase, game);
        }

        player.PassedLeaderWindow = true;
        if (game.Players.All(x => x.PassedLeaderWindow))
        {
            ResistanceRoles.AfterLeaderWindow(game);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResistancePlayCardResult(ResistancePlayCardStatus.Done, game);
    }

    /// <summary>Loads the game and checks that it is started and not over; the status is null when it is.</summary>
    private async Task<(ResistancePlayCardStatus? Status, ResistanceGame? Game)> LoadStartedAsync(
        WriterDbContext dbContext, Guid sessionId, CancellationToken cancellationToken)
    {
        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return (ResistancePlayCardStatus.NoGame, null);
        }

        if (!game.IsStarted)
        {
            return (ResistancePlayCardStatus.NotStarted, game);
        }

        return game.Phase == ResistancePhase.GameOver
            ? (ResistancePlayCardStatus.GameOver, game)
            : (null, game);
    }

    public async Task<ResistancePlayCardResult> ConfirmPlotCardAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

        var (early, game) = await LoadResolvingAsync(dbContext, sessionId, cancellationToken);
        if (early is { } status)
        {
            return new ResistancePlayCardResult(status, game);
        }

        var card = game!.PlotCards.Find(x => x.CardId == cardId);
        if (card is null || !ResistanceRoles.IsImmediate(card.Type)
            || card.Location is not (ResistancePlotCardLocation.Hand or ResistancePlotCardLocation.Played))
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.UnknownCard, game);
        }

        if (card.Location == ResistancePlotCardLocation.Hand || card.IsConfirmed)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.WrongState, game);
        }

        if (callerPlayerId is null || ResistanceRoles.Viewer(card) != callerPlayerId)
        {
            return new ResistancePlayCardResult(ResistancePlayCardStatus.NotAllowed, game);
        }

        card.IsConfirmed = true;
        if (!ResistanceRoles.UnplayedImmediateCards(game).Any() && !ResistanceRoles.UnconfirmedCards(game).Any())
        {
            game.Phase = ResistancePhase.SelectingTeam;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResistancePlayCardResult(ResistancePlayCardStatus.Done, game);
    }

    /// <summary>Loads the game and checks that the immediate cards are being played; the status is null when they are.</summary>
    private async Task<(ResistancePlayCardStatus? Status, ResistanceGame? Game)> LoadResolvingAsync(
        WriterDbContext dbContext, Guid sessionId, CancellationToken cancellationToken)
    {
        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return (ResistancePlayCardStatus.NoGame, null);
        }

        if (!game.IsStarted)
        {
            return (ResistancePlayCardStatus.NotStarted, game);
        }

        if (game.Phase == ResistancePhase.GameOver)
        {
            return (ResistancePlayCardStatus.GameOver, game);
        }

        return game.Phase == ResistancePhase.ResolvingCards
            ? (null, game)
            : (ResistancePlayCardStatus.NotResolving, game);
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

        var opinionMakers = ResistanceRoles.OpinionMakerIds(game);
        if (opinionMakers.Contains(voter.PlayerId))
        {
            if (voter.Vote is not null)
            {
                return new ResistanceVoteResult(ResistanceVoteStatus.VoteLocked, game);
            }
        }
        else if (opinionMakers.Any(id => game.Players.First(x => x.PlayerId == id).Vote is null))
        {
            return new ResistanceVoteResult(ResistanceVoteStatus.OpinionMakersFirst, game);
        }

        voter.Vote = approve;
        if (game.Players.All(x => x.Vote is not null))
        {
            ResistanceRoles.ResolveVotes(game);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResistanceVoteResult(ResistanceVoteStatus.Voted, game);
    }

    public async Task<ResistanceMissionCardResult> PlayMissionCardAsync(
        Guid sessionId, Guid? callerPlayerId, bool success, CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateWriterAsync(cancellationToken);

        var game = await LoadAsync(dbContext, sessionId, cancellationToken);
        if (game is null)
        {
            return new ResistanceMissionCardResult(ResistanceMissionStatus.NoGame, null);
        }

        if (!game.IsStarted)
        {
            return new ResistanceMissionCardResult(ResistanceMissionStatus.NotStarted, game);
        }

        if (game.Phase == ResistancePhase.GameOver)
        {
            return new ResistanceMissionCardResult(ResistanceMissionStatus.GameOver, game);
        }

        var player = game.Players.FirstOrDefault(x => x.PlayerId == callerPlayerId);
        if (player is null)
        {
            return new ResistanceMissionCardResult(ResistanceMissionStatus.NotPlayer, game);
        }

        if (game.Phase != ResistancePhase.PlayingMission)
        {
            return new ResistanceMissionCardResult(ResistanceMissionStatus.NotPlaying, game);
        }

        if (!player.IsOnTeam)
        {
            return new ResistanceMissionCardResult(ResistanceMissionStatus.NotOnTeam, game);
        }

        if (player.MissionCard is not null)
        {
            return new ResistanceMissionCardResult(ResistanceMissionStatus.AlreadyPlayed, game);
        }

        if (!success && player.Role == ResistanceRole.Resistance)
        {
            return new ResistanceMissionCardResult(ResistanceMissionStatus.MustSucceed, game);
        }

        player.MissionCard = success;
        if (ResistanceRoles.AllCardsPlayed(game))
        {
            ResistanceRoles.ResolveMission(game);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResistanceMissionCardResult(ResistanceMissionStatus.Played, game);
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

        if (game.Phase != ResistancePhase.MissionResult)
        {
            return new ResistanceContinueResult(ResistanceContinueStatus.NothingToContinue, game);
        }

        ResistanceRoles.PassLeadership(game);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResistanceContinueResult(ResistanceContinueStatus.Continued, game);
    }
}
