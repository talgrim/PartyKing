using ErrorOr;
using PartyKing.Application.Games.Services.Resistance;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Results;

namespace PartyKing.Application.Tests;

/// <summary>
/// The golden table of the Resistance errors: every repository status and the error it becomes (type, code and
/// message) or null when the status means success. The UI and the tests match on the codes, so a refactor of how the
/// statuses are produced or mapped has to reproduce this table exactly. Each table must name every enum value, so a
/// new status can't slip in without a decision.
/// </summary>
public class ResistanceErrorsTests
{
    private record Expected(ErrorType Type, string Code, string Description);

    private static readonly Expected NoGame = new(ErrorType.NotFound, "Resistance.NotFound", "The session has no Resistance game.");

    private static readonly Expected NotStarted = new(ErrorType.Conflict, "Resistance.NotStarted", "The game has not started yet.");

    private static readonly Expected GameOver = new(ErrorType.Conflict, "Resistance.GameOver", "The game is over.");

    private static void AssertTable<TStatus>(Func<TStatus, Error?> map, Dictionary<TStatus, Expected?> table)
        where TStatus : struct, Enum
    {
        Assert.Equal(
            Enum.GetValues<TStatus>().Select(x => x.ToString()).Order(),
            table.Keys.Select(x => x.ToString()).Order());

        foreach (var (status, expected) in table)
        {
            var error = map(status);
            if (expected is null)
            {
                Assert.Null(error);
                continue;
            }

            Assert.True(error.HasValue, $"{typeof(TStatus).Name}.{status} should be an error");
            Assert.Equal(expected.Type, error.Value.Type);
            Assert.Equal(expected.Code, error.Value.Code);
            Assert.Equal(expected.Description, error.Value.Description);
        }
    }

    [Fact]
    public void SharedErrors_HaveStableCodes()
    {
        Assert.Equal(ErrorType.Conflict, ResistanceErrors.AlreadyCreated.Type);
        Assert.Equal("Resistance.AlreadyCreated", ResistanceErrors.AlreadyCreated.Code);
        Assert.Equal("The session already has a Resistance game.", ResistanceErrors.AlreadyCreated.Description);
    }

    [Theory]
    [InlineData(5, 1, 2)]
    [InlineData(6, 3, 4)]
    [InlineData(10, 4, 5)]
    public void TeamStatus_MapsToErrors(int playerCount, int mission, int teamSize)
    {
        var game = new ResistanceGame { MissionNumber = mission };
        for (var i = 0; i < playerCount; i++)
        {
            game.Players.Add(new ResistancePlayer { PlayerId = Guid.NewGuid(), Order = i });
        }

        AssertTable(
            status => ResistanceErrors.From(status, game),
            new Dictionary<ResistanceTeamStatus, Expected?>
            {
                [ResistanceTeamStatus.NoGame] = NoGame,
                [ResistanceTeamStatus.NotStarted] = NotStarted,
                [ResistanceTeamStatus.GameOver] = GameOver,
                [ResistanceTeamStatus.NotLeader] = new(
                    ErrorType.Forbidden, "Resistance.NotLeader", "Only the mission leader picks the team."),
                [ResistanceTeamStatus.AlreadySubmitted] = new(
                    ErrorType.Conflict, "Resistance.TeamSubmitted", "The team was already submitted."),
                [ResistanceTeamStatus.CardsNotDealt] = new(
                    ErrorType.Conflict, "Resistance.CardsNotDealt", "The leader has to hand out the plot cards first."),
                [ResistanceTeamStatus.WrongSize] = new(
                    ErrorType.Validation,
                    "Resistance.WrongTeamSize",
                    $"The team needs exactly {teamSize} different players."),
                [ResistanceTeamStatus.UnknownPlayer] = new(
                    ErrorType.Validation, "Resistance.UnknownPlayer", "The team has a player who is not in the game."),
                [ResistanceTeamStatus.Selected] = null,
            });
    }

    [Fact]
    public void DealStatus_MapsToErrors()
    {
        AssertTable(
            ResistanceErrors.From,
            new Dictionary<ResistanceDealStatus, Expected?>
            {
                [ResistanceDealStatus.NoGame] = NoGame,
                [ResistanceDealStatus.NotStarted] = NotStarted,
                [ResistanceDealStatus.GameOver] = GameOver,
                [ResistanceDealStatus.NotLeader] = new(
                    ErrorType.Forbidden, "Resistance.NotLeader", "Only the mission leader hands out the plot cards."),
                [ResistanceDealStatus.NotDealing] = new(
                    ErrorType.Conflict, "Resistance.NotDealing", "There are no plot cards to hand out."),
                [ResistanceDealStatus.UnknownCard] = new(
                    ErrorType.Validation,
                    "Resistance.UnknownCard",
                    "A card is not one the leader has to hand out, or is given twice."),
                [ResistanceDealStatus.InvalidReceiver] = new(
                    ErrorType.Validation, "Resistance.InvalidReceiver", "Cards go to other players of the game."),
                [ResistanceDealStatus.Incomplete] = new(
                    ErrorType.Validation, "Resistance.IncompleteDeal", "Every drawn card has to be handed out."),
                [ResistanceDealStatus.Dealt] = null,
            });
    }

    [Fact]
    public void PlayCardStatus_MapsToErrors()
    {
        AssertTable(
            ResistanceErrors.From,
            new Dictionary<ResistancePlayCardStatus, Expected?>
            {
                [ResistancePlayCardStatus.NoGame] = NoGame,
                [ResistancePlayCardStatus.NotStarted] = NotStarted,
                [ResistancePlayCardStatus.GameOver] = GameOver,
                [ResistancePlayCardStatus.NotResolving] = new(
                    ErrorType.Conflict, "Resistance.NotResolving", "No plot cards are being played now."),
                [ResistancePlayCardStatus.NotPlayingMission] = new(
                    ErrorType.Conflict, "Resistance.NotPlayingMission", "The mission cards are not being played now."),
                [ResistancePlayCardStatus.UnknownCard] = new(
                    ErrorType.NotFound, "Resistance.UnknownCard", "There is no such card to play."),
                [ResistancePlayCardStatus.NotAllowed] = new(
                    ErrorType.Forbidden, "Resistance.NotYourCard", "This is not your card to play or to confirm."),
                [ResistancePlayCardStatus.WrongState] = new(
                    ErrorType.Conflict, "Resistance.CardState", "The card was played already, or has not been played yet."),
                [ResistancePlayCardStatus.InvalidTarget] = new(
                    ErrorType.Validation, "Resistance.InvalidTarget", "The card cannot be played on that player."),
                [ResistancePlayCardStatus.WrongPhase] = new(
                    ErrorType.Conflict,
                    "Resistance.CardNotPlayableNow",
                    "That card can't be played in this step of the round."),
                [ResistancePlayCardStatus.NotPlayer] = new(
                    ErrorType.Forbidden, "Resistance.NotPlayer", "Only the players of the game confirm."),
                [ResistancePlayCardStatus.ChoiceRequired] = new(
                    ErrorType.Validation,
                    "Resistance.ChoiceRequired",
                    "Say whether to take the Opinion Maker or a card from the hand."),
                [ResistancePlayCardStatus.Done] = null,
            });
    }

    [Fact]
    public void MissionStatus_MapsToErrors()
    {
        AssertTable(
            ResistanceErrors.From,
            new Dictionary<ResistanceMissionStatus, Expected?>
            {
                [ResistanceMissionStatus.NoGame] = NoGame,
                [ResistanceMissionStatus.NotStarted] = NotStarted,
                [ResistanceMissionStatus.GameOver] = GameOver,
                [ResistanceMissionStatus.NotPlayer] = new(
                    ErrorType.Forbidden, "Resistance.NotPlayer", "Only the players of the game play the mission."),
                [ResistanceMissionStatus.NotPlaying] = new(
                    ErrorType.Conflict, "Resistance.NotPlayingMission", "No approved team is playing a mission."),
                [ResistanceMissionStatus.NotOnTeam] = new(
                    ErrorType.Forbidden, "Resistance.NotOnTeam", "Only the members of the team play the mission."),
                [ResistanceMissionStatus.AlreadyPlayed] = new(
                    ErrorType.Conflict, "Resistance.AlreadyPlayed", "You already played your card."),
                [ResistanceMissionStatus.MustSucceed] = new(
                    ErrorType.Forbidden, "Resistance.MustSucceed", "A member of the resistance can only play Success."),
                [ResistanceMissionStatus.Played] = null,
            });
    }

    [Fact]
    public void ContinueStatus_MapsToErrors()
    {
        AssertTable(
            ResistanceErrors.From,
            new Dictionary<ResistanceContinueStatus, Expected?>
            {
                [ResistanceContinueStatus.NoGame] = NoGame,
                [ResistanceContinueStatus.NotStarted] = NotStarted,
                [ResistanceContinueStatus.GameOver] = GameOver,
                [ResistanceContinueStatus.NotAllowed] = new(
                    ErrorType.Forbidden, "Resistance.NotAllowed", "Only the leader, the host or an admin continues."),
                [ResistanceContinueStatus.NothingToContinue] = new(
                    ErrorType.Conflict, "Resistance.NothingToContinue", "There is no finished mission to move on from."),
                [ResistanceContinueStatus.Continued] = null,
            });
    }
}
