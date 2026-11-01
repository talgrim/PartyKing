namespace PartyKing.Domain.Entities.Games;

/// <summary>
/// Why a Resistance action did not happen. One member per distinct answer the caller gets: two actions that refuse
/// for a similar reason in different words have their own member (for example the three "not a player" ones), because
/// the error codes and messages built from them are matched by the UI.
/// </summary>
public enum ResistanceFailure
{
    NoGame,
    NotStarted,
    GameOver,

    /// <summary>The rules are refreshed only before the game is open to guests.</summary>
    RulesLocked,

    /// <summary>The plot cards are switched only before the game starts.</summary>
    PlotCardsLocked,

    NotOpen,
    AlreadyStarted,

    /// <summary>Fewer than <see cref="ResistanceGame.MinPlayers"/> players joined.</summary>
    NotEnoughPlayers,

    /// <summary>The game is still being played; only a finished one restarts.</summary>
    NotOver,

    /// <summary>The caller is not the player leading the mission, so they don't pick the team.</summary>
    NotLeaderToPick,

    TeamSubmitted,

    /// <summary>The leader still has to hand out the plot cards.</summary>
    CardsNotDealt,

    /// <summary>Not exactly <see cref="ResistanceRules.TeamSize"/> distinct players.</summary>
    WrongTeamSize,

    UnknownTeamPlayer,

    /// <summary>The caller is not the player leading the mission, so they don't hand out the plot cards.</summary>
    NotLeaderToDeal,

    /// <summary>No cards are waiting to be handed out (add-on off, or already dealt).</summary>
    NotDealing,

    /// <summary>A card that was not drawn, is kept by the leader, or is given twice.</summary>
    UnknownDealCard,

    /// <summary>A receiver that is not a player, or the leader themselves.</summary>
    InvalidReceiver,

    /// <summary>Some of the cards to hand out are left.</summary>
    IncompleteDeal,

    /// <summary>The immediate cards are not being played.</summary>
    NotResolving,

    /// <summary>In the Spotlight is played while the mission cards are played, and the mission is not.</summary>
    NotPlayingMission,

    /// <summary>No such card, or it is not one that can be played this way.</summary>
    UnknownCard,

    /// <summary>The caller does not hold the card (to play it), or is not the one it shows the identity card to.</summary>
    NotYourCard,

    /// <summary>The card was played already (to play it), or has not been played yet (to confirm it).</summary>
    CardState,

    /// <summary>The target is not a legal one for the card.</summary>
    InvalidTarget,

    /// <summary>The card can't be played in this step of the round.</summary>
    CardNotPlayableNow,

    /// <summary>The caller did not join the game (a host or admin who only watches), so they don't confirm.</summary>
    NotPlayerToConfirm,

    /// <summary>The target has a face-up Opinion Maker and cards in hand, and the caller did not say which to take.</summary>
    ChoiceRequired,

    /// <summary>The caller did not join the game (a host or admin who only watches), so they don't vote.</summary>
    NotPlayerToVote,

    /// <summary>No team is up for a vote: it is not picked yet, or the votes are already in.</summary>
    NotVoting,

    /// <summary>The caller is an Opinion Maker and already revealed their vote, which can't be changed.</summary>
    VoteLocked,

    /// <summary>An Opinion Maker in play has not revealed their vote yet, so the others wait.</summary>
    OpinionMakersFirst,

    /// <summary>The caller did not join the game (a host or admin who only watches), so they don't play the mission.</summary>
    NotPlayerToPlay,

    /// <summary>No approved team is playing a mission.</summary>
    NoApprovedTeam,

    /// <summary>The caller is not on the team.</summary>
    NotOnTeam,

    AlreadyPlayed,

    /// <summary>A member of the resistance may only play Success.</summary>
    MustSucceed,

    /// <summary>Neither the leader nor a manager, so they don't continue.</summary>
    NotAllowedToContinue,

    /// <summary>The votes are not in yet, or the approved team's mission is still being played.</summary>
    NothingToContinue,
}
