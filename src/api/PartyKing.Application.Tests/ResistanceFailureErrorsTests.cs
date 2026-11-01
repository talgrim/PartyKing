using ErrorOr;
using PartyKing.Application.Games.Services.Resistance;
using PartyKing.Domain.Entities.Games;

namespace PartyKing.Application.Tests;

/// <summary>
/// The golden table by failure: the error (type, code and message) each <see cref="ResistanceFailure"/> becomes. The
/// UI and the tests match on the codes. Every failure is in the table.
/// </summary>
public class ResistanceFailureErrorsTests
{
    internal record Expected(ErrorType Type, string Code, string Description);

    internal static readonly Dictionary<ResistanceFailure, Expected> Golden = new()
    {
        [ResistanceFailure.RulesLocked] = new(
            ErrorType.Conflict,
            "Resistance.RulesLocked",
            "The rules can only be refreshed before the game is opened."),
        [ResistanceFailure.PlotCardsLocked] = new(
            ErrorType.Conflict,
            "Resistance.RulesLocked",
            "The plot cards can only be switched before the game starts."),
        [ResistanceFailure.NotStarted] = new(ErrorType.Conflict, "Resistance.NotStarted", "The game has not started yet."),
        [ResistanceFailure.NotOpen] = new(ErrorType.Conflict, "Resistance.NotOpen", "The game is not open to guests yet."),
        [ResistanceFailure.AlreadyStarted] = new(
            ErrorType.Conflict, "Resistance.AlreadyStarted", "The game has already started."),
        [ResistanceFailure.NotEnoughPlayers] = new(
            ErrorType.Conflict, "Resistance.NotEnoughPlayers", "The game needs at least 5 players to start."),
        [ResistanceFailure.NotOver] = new(ErrorType.Conflict, "Resistance.NotOver", "The game is still being played."),
        [ResistanceFailure.NotLeaderToPick] = new(
            ErrorType.Forbidden, "Resistance.NotLeader", "Only the mission leader picks the team."),
        [ResistanceFailure.TeamSubmitted] = new(
            ErrorType.Conflict, "Resistance.TeamSubmitted", "The team was already submitted."),
        [ResistanceFailure.NotSelectingTeam] = new(
            ErrorType.Conflict, "Resistance.NotSelectingTeam", "The team can't be picked right now."),
        [ResistanceFailure.CardsNotDealt] = new(
            ErrorType.Conflict, "Resistance.CardsNotDealt", "The leader has to hand out the plot cards first."),
        [ResistanceFailure.WrongTeamSize] = new(
            ErrorType.Validation, "Resistance.WrongTeamSize", "The team needs exactly 2 different players."),
        [ResistanceFailure.UnknownTeamPlayer] = new(
            ErrorType.Validation, "Resistance.UnknownPlayer", "The team has a player who is not in the game."),
        [ResistanceFailure.NotPlayerToVote] = new(
            ErrorType.Forbidden, "Resistance.NotPlayer", "Only the players of the game vote."),
        [ResistanceFailure.NotVoting] = new(ErrorType.Conflict, "Resistance.NotVoting", "No team is up for a vote."),
        [ResistanceFailure.VoteLocked] = new(
            ErrorType.Conflict, "Resistance.VoteLocked", "An Opinion Maker's vote can't be changed."),
        [ResistanceFailure.OpinionMakersFirst] = new(
            ErrorType.Conflict, "Resistance.OpinionMakersFirst", "The Opinion Makers vote first."),
        [ResistanceFailure.NotPlayerToPlay] = new(
            ErrorType.Forbidden, "Resistance.NotPlayer", "Only the players of the game play the mission."),
        [ResistanceFailure.NoApprovedTeam] = new(
            ErrorType.Conflict, "Resistance.NotPlayingMission", "No approved team is playing a mission."),
        [ResistanceFailure.NotOnTeam] = new(
            ErrorType.Forbidden, "Resistance.NotOnTeam", "Only the members of the team play the mission."),
        [ResistanceFailure.AlreadyPlayed] = new(
            ErrorType.Conflict, "Resistance.AlreadyPlayed", "You already played your card."),
        [ResistanceFailure.MustSucceed] = new(
            ErrorType.Forbidden, "Resistance.MustSucceed", "A member of the resistance can only play Success."),
        [ResistanceFailure.NotAllowedToContinue] = new(
            ErrorType.Forbidden, "Resistance.NotAllowed", "Only the leader, the host or an admin continues."),
        [ResistanceFailure.NothingToContinue] = new(
            ErrorType.Conflict, "Resistance.NothingToContinue", "There is no finished mission to move on from."),
        [ResistanceFailure.GameOver] = new(ErrorType.Conflict, "Resistance.GameOver", "The game is over."),
        [ResistanceFailure.NoGame] = new(ErrorType.NotFound, "Resistance.NotFound", "The session has no Resistance game."),
        [ResistanceFailure.NotLeaderToDeal] = new(
            ErrorType.Forbidden, "Resistance.NotLeader", "Only the mission leader hands out the plot cards."),
        [ResistanceFailure.NotDealing] = new(
            ErrorType.Conflict, "Resistance.NotDealing", "There are no plot cards to hand out."),
        [ResistanceFailure.UnknownDealCard] = new(
            ErrorType.Validation,
            "Resistance.UnknownCard",
            "A card is not one the leader has to hand out, or is given twice."),
        [ResistanceFailure.InvalidReceiver] = new(
            ErrorType.Validation, "Resistance.InvalidReceiver", "Cards go to other players of the game."),
        [ResistanceFailure.IncompleteDeal] = new(
            ErrorType.Validation, "Resistance.IncompleteDeal", "Every drawn card has to be handed out."),
        [ResistanceFailure.NotResolving] = new(
            ErrorType.Conflict, "Resistance.NotResolving", "No plot cards are being played now."),
        [ResistanceFailure.NotPlayingMission] = new(
            ErrorType.Conflict, "Resistance.NotPlayingMission", "The mission cards are not being played now."),
        [ResistanceFailure.UnknownCard] = new(
            ErrorType.NotFound, "Resistance.UnknownCard", "There is no such card to play."),
        [ResistanceFailure.NotYourCard] = new(
            ErrorType.Forbidden, "Resistance.NotYourCard", "This is not your card to play or to confirm."),
        [ResistanceFailure.CardState] = new(
            ErrorType.Conflict, "Resistance.CardState", "The card was played already, or has not been played yet."),
        [ResistanceFailure.InvalidTarget] = new(
            ErrorType.Validation, "Resistance.InvalidTarget", "The card cannot be played on that player."),
        [ResistanceFailure.CardNotPlayableNow] = new(
            ErrorType.Conflict,
            "Resistance.CardNotPlayableNow",
            "That card can't be played in this step of the round."),
        [ResistanceFailure.NotPlayerToConfirm] = new(
            ErrorType.Forbidden, "Resistance.NotPlayer", "Only the players of the game confirm."),
        [ResistanceFailure.ChoiceRequired] = new(
            ErrorType.Validation,
            "Resistance.ChoiceRequired",
            "Say whether to take the Opinion Maker or a card from the hand."),
    };

    [Fact]
    public void EachFailureOfTheTable_BecomesItsError()
    {
        // Five players in the first mission, for the message of a wrong team size
        var game = new ResistanceGame();
        for (var i = 0; i < 5; i++)
        {
            game.Players.Add(new ResistancePlayer { PlayerId = Guid.NewGuid(), Order = i });
        }


        foreach (var (failure, expected) in Golden)
        {
            var error = ResistanceErrors.From(ResistanceOutcome.Failed(failure, game));

            Assert.True(error.HasValue, $"{failure} should be an error");
            Assert.Equal(expected.Type, error.Value.Type);
            Assert.Equal(expected.Code, error.Value.Code);
            Assert.Equal(expected.Description, error.Value.Description);
        }
    }

    [Fact]
    public void EveryFailure_IsInTheTable()
    {
        Assert.Equal(Enum.GetValues<ResistanceFailure>().Order(), Golden.Keys.Order());
    }
}
