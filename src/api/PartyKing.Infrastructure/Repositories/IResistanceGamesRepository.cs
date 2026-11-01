using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Results;

namespace PartyKing.Infrastructure.Repositories;

public interface IResistanceGamesRepository : ILobbyGamesRepository<ResistanceGame, ResistancePlayer>
{
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
}
