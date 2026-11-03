using ErrorOr;
using PartyKing.Application.Games.Services.Resistance;
using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Application.Tests;

/// <summary>The error of a <see cref="ResistanceOutcome"/>; the table by failure is <see cref="ResistanceFailureErrorsTests"/>.</summary>
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
}
