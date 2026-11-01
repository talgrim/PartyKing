using ErrorOr;
using PartyKing.Application.Games.Services.Resistance;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Results;

namespace PartyKing.Application.Tests;

/// <summary>
/// The error of a <see cref="ResistanceOutcome"/>. The last test proves that every status the repositories answer
/// with today (pinned by <see cref="ResistanceErrorsTests"/>) maps to the same error through its failure, and that no
/// failure is left without a status it replaces, so the services can switch from the statuses to the outcome without
/// changing a code or a message.
/// </summary>
public class ResistanceOutcomeErrorsTests
{
    private static ResistanceGame NewGame(int playerCount = 5, int mission = 1)
    {
        var game = new ResistanceGame { MissionNumber = mission };
        for (var i = 0; i < playerCount; i++)
        {
            game.Players.Add(new ResistancePlayer { PlayerId = Guid.NewGuid(), Order = i });
        }

        return game;
    }

    private static Error? Map(ResistanceFailure? failure, ResistanceGame game)
    {
        return ResistanceErrors.From(failure is { } f ? ResistanceOutcome.Failed(f, game) : ResistanceOutcome.Success(game));
    }

    [Fact]
    public void ASuccessfulOutcome_HasNoErrorAndChangedTheGame()
    {
        var outcome = ResistanceOutcome.Success(NewGame());

        Assert.Null(ResistanceErrors.From(outcome));
        Assert.True(outcome.Changed);
    }

    [Fact]
    public void AFailedOutcome_ChangedNothing()
    {
        var outcome = ResistanceOutcome.Failed(ResistanceFailure.NotVoting, NewGame());

        Assert.False(outcome.Changed);
        Assert.Equal("Resistance.NotVoting", ResistanceErrors.From(outcome)!.Value.Code);
    }

    [Fact]
    public void TheNoGameOutcome_HasNoGameAndTheNotFoundError()
    {
        var outcome = ResistanceOutcome.NoGame;

        Assert.Null(outcome.Game);
        Assert.False(outcome.Changed);
        Assert.Equal(ResistanceErrors.NoGame, ResistanceErrors.From(outcome));
    }

    [Theory]
    [InlineData(5, 1, 2)]
    [InlineData(6, 3, 4)]
    [InlineData(10, 4, 5)]
    public void AWrongTeamSize_NamesTheSizeTheRulesRequire(int playerCount, int mission, int teamSize)
    {
        var error = Map(ResistanceFailure.WrongTeamSize, NewGame(playerCount, mission));

        Assert.Equal($"The team needs exactly {teamSize} different players.", error!.Value.Description);
    }

    [Fact]
    public void EveryFailure_HasAnError()
    {
        var game = NewGame();

        foreach (var failure in Enum.GetValues<ResistanceFailure>())
        {
            var error = Map(failure, game);

            Assert.True(error.HasValue, $"{failure} should be an error");
            Assert.False(string.IsNullOrWhiteSpace(error.Value.Code));
            Assert.False(string.IsNullOrWhiteSpace(error.Value.Description));
        }
    }

    [Fact]
    public void TheStatusesOfTheRepositories_MapToTheSameErrorsThroughTheirFailures()
    {
        var game = NewGame();
        // The failures of migrated actions have no status left; the golden table pins them instead
        var covered = ResistanceFailureErrorsTests.Golden.Keys.ToHashSet();

        void Check<TStatus>(Func<TStatus, Error?> old, Dictionary<TStatus, ResistanceFailure?> translation)
            where TStatus : struct, Enum
        {
            Assert.Equal(
                Enum.GetValues<TStatus>().Select(x => x.ToString()).Order(),
                translation.Keys.Select(x => x.ToString()).Order());

            foreach (var (status, failure) in translation)
            {
                Assert.Equal(old(status), Map(failure, game));
                if (failure is { } f)
                {
                    covered.Add(f);
                }
            }
        }

        Check<ResistanceTeamStatus>(
            x => ResistanceErrors.From(x, game),
            new()
            {
                [ResistanceTeamStatus.NoGame] = ResistanceFailure.NoGame,
                [ResistanceTeamStatus.NotStarted] = ResistanceFailure.NotStarted,
                [ResistanceTeamStatus.GameOver] = ResistanceFailure.GameOver,
                [ResistanceTeamStatus.NotLeader] = ResistanceFailure.NotLeaderToPick,
                [ResistanceTeamStatus.AlreadySubmitted] = ResistanceFailure.TeamSubmitted,
                [ResistanceTeamStatus.CardsNotDealt] = ResistanceFailure.CardsNotDealt,
                [ResistanceTeamStatus.WrongSize] = ResistanceFailure.WrongTeamSize,
                [ResistanceTeamStatus.UnknownPlayer] = ResistanceFailure.UnknownTeamPlayer,
                [ResistanceTeamStatus.Selected] = null,
            });
        Check<ResistanceDealStatus>(
            ResistanceErrors.From,
            new()
            {
                [ResistanceDealStatus.NoGame] = ResistanceFailure.NoGame,
                [ResistanceDealStatus.NotStarted] = ResistanceFailure.NotStarted,
                [ResistanceDealStatus.GameOver] = ResistanceFailure.GameOver,
                [ResistanceDealStatus.NotLeader] = ResistanceFailure.NotLeaderToDeal,
                [ResistanceDealStatus.NotDealing] = ResistanceFailure.NotDealing,
                [ResistanceDealStatus.UnknownCard] = ResistanceFailure.UnknownDealCard,
                [ResistanceDealStatus.InvalidReceiver] = ResistanceFailure.InvalidReceiver,
                [ResistanceDealStatus.Incomplete] = ResistanceFailure.IncompleteDeal,
                [ResistanceDealStatus.Dealt] = null,
            });
        Check<ResistancePlayCardStatus>(
            ResistanceErrors.From,
            new()
            {
                [ResistancePlayCardStatus.NoGame] = ResistanceFailure.NoGame,
                [ResistancePlayCardStatus.NotStarted] = ResistanceFailure.NotStarted,
                [ResistancePlayCardStatus.GameOver] = ResistanceFailure.GameOver,
                [ResistancePlayCardStatus.NotResolving] = ResistanceFailure.NotResolving,
                [ResistancePlayCardStatus.NotPlayingMission] = ResistanceFailure.NotPlayingMission,
                [ResistancePlayCardStatus.UnknownCard] = ResistanceFailure.UnknownCard,
                [ResistancePlayCardStatus.NotAllowed] = ResistanceFailure.NotYourCard,
                [ResistancePlayCardStatus.WrongState] = ResistanceFailure.CardState,
                [ResistancePlayCardStatus.InvalidTarget] = ResistanceFailure.InvalidTarget,
                [ResistancePlayCardStatus.WrongPhase] = ResistanceFailure.CardNotPlayableNow,
                [ResistancePlayCardStatus.NotPlayer] = ResistanceFailure.NotPlayerToConfirm,
                [ResistancePlayCardStatus.ChoiceRequired] = ResistanceFailure.ChoiceRequired,
                [ResistancePlayCardStatus.Done] = null,
            });
        Check<ResistanceMissionStatus>(
            ResistanceErrors.From,
            new()
            {
                [ResistanceMissionStatus.NoGame] = ResistanceFailure.NoGame,
                [ResistanceMissionStatus.NotStarted] = ResistanceFailure.NotStarted,
                [ResistanceMissionStatus.GameOver] = ResistanceFailure.GameOver,
                [ResistanceMissionStatus.NotPlayer] = ResistanceFailure.NotPlayerToPlay,
                [ResistanceMissionStatus.NotPlaying] = ResistanceFailure.NoApprovedTeam,
                [ResistanceMissionStatus.NotOnTeam] = ResistanceFailure.NotOnTeam,
                [ResistanceMissionStatus.AlreadyPlayed] = ResistanceFailure.AlreadyPlayed,
                [ResistanceMissionStatus.MustSucceed] = ResistanceFailure.MustSucceed,
                [ResistanceMissionStatus.Played] = null,
            });
        Check<ResistanceContinueStatus>(
            ResistanceErrors.From,
            new()
            {
                [ResistanceContinueStatus.NoGame] = ResistanceFailure.NoGame,
                [ResistanceContinueStatus.NotStarted] = ResistanceFailure.NotStarted,
                [ResistanceContinueStatus.GameOver] = ResistanceFailure.GameOver,
                [ResistanceContinueStatus.NotAllowed] = ResistanceFailure.NotAllowedToContinue,
                [ResistanceContinueStatus.NothingToContinue] = ResistanceFailure.NothingToContinue,
                [ResistanceContinueStatus.Continued] = null,
            });

        // No failure is left without a status it replaces
        Assert.Equal(Enum.GetValues<ResistanceFailure>().Order(), covered.Order());
    }
}
