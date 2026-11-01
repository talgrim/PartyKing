using ErrorOr;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Results;

namespace PartyKing.Application.Games.Services.Resistance;

/// <summary>
/// The errors of the Resistance game: the ones every action shares, and one <c>From</c> overload per repository status
/// that turns it into an error, or null when the status means the action succeeded. Codes are matched by the UI.
/// </summary>
internal static class ResistanceErrors
{
    public const string GameName = "Resistance";

    public static Error NoGame => Error.NotFound($"{GameName}.NotFound", $"The session has no {GameName} game.");

    public static Error AlreadyCreated =>
        Error.Conflict("Resistance.AlreadyCreated", "The session already has a Resistance game.");

    public static Error NotStarted => Error.Conflict("Resistance.NotStarted", "The game has not started yet.");

    public static Error GameOver => Error.Conflict("Resistance.GameOver", "The game is over.");

    /// <summary>The error of an action's outcome, or null when the action happened. Codes are matched by the UI.</summary>
    public static Error? From(ResistanceOutcome outcome)
    {
        return outcome.Failure is { } failure ? From(failure, outcome.Game) : null;
    }

    private static Error From(ResistanceFailure failure, ResistanceGame? game) => failure switch
    {
        ResistanceFailure.NoGame => NoGame,
        ResistanceFailure.NotStarted => NotStarted,
        ResistanceFailure.GameOver => GameOver,
        ResistanceFailure.RulesLocked => Error.Conflict(
            "Resistance.RulesLocked", "The rules can only be refreshed before the game is opened."),
        ResistanceFailure.PlotCardsLocked => Error.Conflict(
            "Resistance.RulesLocked", "The plot cards can only be switched before the game starts."),
        ResistanceFailure.NotOpen => Error.Conflict("Resistance.NotOpen", "The game is not open to guests yet."),
        ResistanceFailure.AlreadyStarted => Error.Conflict("Resistance.AlreadyStarted", "The game has already started."),
        ResistanceFailure.NotEnoughPlayers => Error.Conflict(
            "Resistance.NotEnoughPlayers",
            $"The game needs at least {ResistanceGame.MinPlayers} players to start."),
        ResistanceFailure.NotOver => Error.Conflict("Resistance.NotOver", "The game is still being played."),
        ResistanceFailure.NotLeaderToPick => Error.Forbidden(
            "Resistance.NotLeader", "Only the mission leader picks the team."),
        ResistanceFailure.TeamSubmitted => Error.Conflict("Resistance.TeamSubmitted", "The team was already submitted."),
        ResistanceFailure.CardsNotDealt => Error.Conflict(
            "Resistance.CardsNotDealt", "The leader has to hand out the plot cards first."),
        ResistanceFailure.WrongTeamSize => Error.Validation(
            "Resistance.WrongTeamSize",
            $"The team needs exactly {game!.Rules.TeamSize(game.Players.Count, game.MissionNumber)} different players."),
        ResistanceFailure.UnknownTeamPlayer => Error.Validation(
            "Resistance.UnknownPlayer", "The team has a player who is not in the game."),
        ResistanceFailure.NotLeaderToDeal => Error.Forbidden(
            "Resistance.NotLeader", "Only the mission leader hands out the plot cards."),
        ResistanceFailure.NotDealing => Error.Conflict("Resistance.NotDealing", "There are no plot cards to hand out."),
        ResistanceFailure.UnknownDealCard => Error.Validation(
            "Resistance.UnknownCard", "A card is not one the leader has to hand out, or is given twice."),
        ResistanceFailure.InvalidReceiver => Error.Validation(
            "Resistance.InvalidReceiver", "Cards go to other players of the game."),
        ResistanceFailure.IncompleteDeal => Error.Validation(
            "Resistance.IncompleteDeal", "Every drawn card has to be handed out."),
        ResistanceFailure.NotResolving => Error.Conflict("Resistance.NotResolving", "No plot cards are being played now."),
        ResistanceFailure.NotPlayingMission => Error.Conflict(
            "Resistance.NotPlayingMission", "The mission cards are not being played now."),
        ResistanceFailure.UnknownCard => Error.NotFound("Resistance.UnknownCard", "There is no such card to play."),
        ResistanceFailure.NotYourCard => Error.Forbidden(
            "Resistance.NotYourCard", "This is not your card to play or to confirm."),
        ResistanceFailure.CardState => Error.Conflict(
            "Resistance.CardState", "The card was played already, or has not been played yet."),
        ResistanceFailure.InvalidTarget => Error.Validation(
            "Resistance.InvalidTarget", "The card cannot be played on that player."),
        ResistanceFailure.CardNotPlayableNow => Error.Conflict(
            "Resistance.CardNotPlayableNow", "That card can't be played in this step of the round."),
        ResistanceFailure.NotPlayerToConfirm => Error.Forbidden(
            "Resistance.NotPlayer", "Only the players of the game confirm."),
        ResistanceFailure.ChoiceRequired => Error.Validation(
            "Resistance.ChoiceRequired", "Say whether to take the Opinion Maker or a card from the hand."),
        ResistanceFailure.NotPlayerToVote => Error.Forbidden("Resistance.NotPlayer", "Only the players of the game vote."),
        ResistanceFailure.NotVoting => Error.Conflict("Resistance.NotVoting", "No team is up for a vote."),
        ResistanceFailure.VoteLocked => Error.Conflict("Resistance.VoteLocked", "An Opinion Maker's vote can't be changed."),
        ResistanceFailure.OpinionMakersFirst => Error.Conflict(
            "Resistance.OpinionMakersFirst", "The Opinion Makers vote first."),
        ResistanceFailure.NotPlayerToPlay => Error.Forbidden(
            "Resistance.NotPlayer", "Only the players of the game play the mission."),
        ResistanceFailure.NoApprovedTeam => Error.Conflict(
            "Resistance.NotPlayingMission", "No approved team is playing a mission."),
        ResistanceFailure.NotOnTeam => Error.Forbidden(
            "Resistance.NotOnTeam", "Only the members of the team play the mission."),
        ResistanceFailure.AlreadyPlayed => Error.Conflict("Resistance.AlreadyPlayed", "You already played your card."),
        ResistanceFailure.MustSucceed => Error.Forbidden(
            "Resistance.MustSucceed", "A member of the resistance can only play Success."),
        ResistanceFailure.NotAllowedToContinue => Error.Forbidden(
            "Resistance.NotAllowed", "Only the leader, the host or an admin continues."),
        ResistanceFailure.NothingToContinue => Error.Conflict(
            "Resistance.NothingToContinue", "There is no finished mission to move on from."),
        _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, "No error is defined for this failure."),
    };

    public static Error? From(ResistanceStartStatus status) => status switch
    {
        ResistanceStartStatus.NoGame => NoGame,
        ResistanceStartStatus.NotOpen => Error.Conflict("Resistance.NotOpen", "The game is not open to guests yet."),
        ResistanceStartStatus.AlreadyStarted => Error.Conflict("Resistance.AlreadyStarted", "The game has already started."),
        ResistanceStartStatus.NotEnoughPlayers => Error.Conflict(
            "Resistance.NotEnoughPlayers",
            $"The game needs at least {ResistanceGame.MinPlayers} players to start."),
        _ => null,
    };

    public static Error? From(ResistanceRestartStatus status) => status switch
    {
        ResistanceRestartStatus.NoGame => NoGame,
        ResistanceRestartStatus.NotStarted => NotStarted,
        ResistanceRestartStatus.NotOver => Error.Conflict("Resistance.NotOver", "The game is still being played."),
        _ => null,
    };

    public static Error? From(ResistanceTeamStatus status, ResistanceGame? game) => status switch
    {
        ResistanceTeamStatus.NoGame => NoGame,
        ResistanceTeamStatus.NotStarted => NotStarted,
        ResistanceTeamStatus.GameOver => GameOver,
        ResistanceTeamStatus.NotLeader => Error.Forbidden("Resistance.NotLeader", "Only the mission leader picks the team."),
        ResistanceTeamStatus.AlreadySubmitted => Error.Conflict("Resistance.TeamSubmitted", "The team was already submitted."),
        ResistanceTeamStatus.CardsNotDealt => Error.Conflict(
            "Resistance.CardsNotDealt", "The leader has to hand out the plot cards first."),
        ResistanceTeamStatus.WrongSize => Error.Validation(
            "Resistance.WrongTeamSize",
            $"The team needs exactly {game!.Rules.TeamSize(game.Players.Count, game.MissionNumber)} different players."),
        ResistanceTeamStatus.UnknownPlayer => Error.Validation(
            "Resistance.UnknownPlayer", "The team has a player who is not in the game."),
        _ => null,
    };

    public static Error? From(ResistanceDealStatus status) => status switch
    {
        ResistanceDealStatus.NoGame => NoGame,
        ResistanceDealStatus.NotStarted => NotStarted,
        ResistanceDealStatus.GameOver => GameOver,
        ResistanceDealStatus.NotLeader => Error.Forbidden(
            "Resistance.NotLeader", "Only the mission leader hands out the plot cards."),
        ResistanceDealStatus.NotDealing => Error.Conflict("Resistance.NotDealing", "There are no plot cards to hand out."),
        ResistanceDealStatus.UnknownCard => Error.Validation(
            "Resistance.UnknownCard", "A card is not one the leader has to hand out, or is given twice."),
        ResistanceDealStatus.InvalidReceiver => Error.Validation(
            "Resistance.InvalidReceiver", "Cards go to other players of the game."),
        ResistanceDealStatus.Incomplete => Error.Validation(
            "Resistance.IncompleteDeal", "Every drawn card has to be handed out."),
        _ => null,
    };

    public static Error? From(ResistancePlayCardStatus status) => status switch
    {
        ResistancePlayCardStatus.NoGame => NoGame,
        ResistancePlayCardStatus.NotStarted => NotStarted,
        ResistancePlayCardStatus.GameOver => GameOver,
        ResistancePlayCardStatus.NotResolving => Error.Conflict(
            "Resistance.NotResolving", "No plot cards are being played now."),
        ResistancePlayCardStatus.NotPlayer => Error.Forbidden(
            "Resistance.NotPlayer", "Only the players of the game confirm."),
        ResistancePlayCardStatus.WrongPhase => Error.Conflict(
            "Resistance.CardNotPlayableNow", "That card can't be played in this step of the round."),
        ResistancePlayCardStatus.ChoiceRequired => Error.Validation(
            "Resistance.ChoiceRequired", "Say whether to take the Opinion Maker or a card from the hand."),
        ResistancePlayCardStatus.NotPlayingMission => Error.Conflict(
            "Resistance.NotPlayingMission", "The mission cards are not being played now."),
        ResistancePlayCardStatus.UnknownCard => Error.NotFound("Resistance.UnknownCard", "There is no such card to play."),
        ResistancePlayCardStatus.NotAllowed => Error.Forbidden(
            "Resistance.NotYourCard", "This is not your card to play or to confirm."),
        ResistancePlayCardStatus.WrongState => Error.Conflict(
            "Resistance.CardState", "The card was played already, or has not been played yet."),
        ResistancePlayCardStatus.InvalidTarget => Error.Validation(
            "Resistance.InvalidTarget", "The card cannot be played on that player."),
        _ => null,
    };

    public static Error? From(ResistanceVoteStatus status) => status switch
    {
        ResistanceVoteStatus.NoGame => NoGame,
        ResistanceVoteStatus.NotStarted => NotStarted,
        ResistanceVoteStatus.GameOver => GameOver,
        ResistanceVoteStatus.NotPlayer => Error.Forbidden("Resistance.NotPlayer", "Only the players of the game vote."),
        ResistanceVoteStatus.NotVoting => Error.Conflict("Resistance.NotVoting", "No team is up for a vote."),
        ResistanceVoteStatus.VoteLocked => Error.Conflict(
            "Resistance.VoteLocked", "An Opinion Maker's vote can't be changed."),
        ResistanceVoteStatus.OpinionMakersFirst => Error.Conflict(
            "Resistance.OpinionMakersFirst", "The Opinion Makers vote first."),
        _ => null,
    };

    public static Error? From(ResistanceMissionStatus status) => status switch
    {
        ResistanceMissionStatus.NoGame => NoGame,
        ResistanceMissionStatus.NotStarted => NotStarted,
        ResistanceMissionStatus.GameOver => GameOver,
        ResistanceMissionStatus.NotPlayer => Error.Forbidden(
            "Resistance.NotPlayer", "Only the players of the game play the mission."),
        ResistanceMissionStatus.NotPlaying => Error.Conflict(
            "Resistance.NotPlayingMission", "No approved team is playing a mission."),
        ResistanceMissionStatus.NotOnTeam => Error.Forbidden(
            "Resistance.NotOnTeam", "Only the members of the team play the mission."),
        ResistanceMissionStatus.AlreadyPlayed => Error.Conflict("Resistance.AlreadyPlayed", "You already played your card."),
        ResistanceMissionStatus.MustSucceed => Error.Forbidden(
            "Resistance.MustSucceed", "A member of the resistance can only play Success."),
        _ => null,
    };

    public static Error? From(ResistanceContinueStatus status) => status switch
    {
        ResistanceContinueStatus.NoGame => NoGame,
        ResistanceContinueStatus.NotStarted => NotStarted,
        ResistanceContinueStatus.GameOver => GameOver,
        ResistanceContinueStatus.NotAllowed => Error.Forbidden(
            "Resistance.NotAllowed", "Only the leader, the host or an admin continues."),
        ResistanceContinueStatus.NothingToContinue => Error.Conflict(
            "Resistance.NothingToContinue", "There is no finished mission to move on from."),
        _ => null,
    };
}
