using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Results;

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
    Task<ResistanceRestartResult> RestartAsync(Guid sessionId, CancellationToken cancellationToken);

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
    /// After a mission is played (until "Dalej") the holder of Keeping a Close Eye looks at one team member's mission
    /// card; a card can be looked at by one player only and never your own. The play is public, the card is not.
    /// </summary>
    Task<ResistancePlayCardResult> PeekMissionCardAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, Guid targetPlayerId, CancellationToken cancellationToken);

    /// <summary>
    /// In the confidence window the holder of No Confidence cancels the approved team: it counts as rejected (the fifth
    /// ends the game) and the lead passes on.
    /// </summary>
    Task<ResistancePlayCardResult> PlayNoConfidenceAsync(
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
