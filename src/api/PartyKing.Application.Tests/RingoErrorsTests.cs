using ErrorOr;
using PartyKing.Application.Games.Services.Ringo;
using PartyKing.Domain.Entities.Games;

namespace PartyKing.Application.Tests;

/// <summary>
/// The golden table of the Ringo errors by failure: type, code and message, which the UI and the tests match on. An
/// accepted outcome has no error, whether or not it changed the game.
/// </summary>
public class RingoErrorsTests
{
    private record Expected(ErrorType Type, string Code, string Description);

    private static readonly Dictionary<RingoFailure, Expected> Golden = new()
    {
        [RingoFailure.NoGame] = new(ErrorType.NotFound, "Ringo.NotFound", "The session has no Ringo game."),
        [RingoFailure.NotYourTurn] = new(ErrorType.Conflict, "Ringo.NotYourTurn", "It is another player's turn."),
        [RingoFailure.GameOver] = new(ErrorType.Conflict, "Ringo.GameOver", "The fourth king ended the game."),
        [RingoFailure.CardPending] = new(
            ErrorType.Conflict, "Ringo.CardPending", "Continue past the flipped card before flipping another."),
    };

    [Fact]
    public void EveryFailure_IsInTheTable()
    {
        Assert.Equal(Enum.GetValues<RingoFailure>().Order(), Golden.Keys.Order());
    }

    [Fact]
    public void EachFailure_BecomesItsError()
    {
        var game = new RingoGame();

        foreach (var (failure, expected) in Golden)
        {
            var error = RingoErrors.From(RingoOutcome.Failed(failure, game));

            Assert.True(error.HasValue, $"{failure} should be an error");
            Assert.Equal(expected.Type, error.Value.Type);
            Assert.Equal(expected.Code, error.Value.Code);
            Assert.Equal(expected.Description, error.Value.Description);
        }
    }

    [Fact]
    public void TheNoGameOutcome_IsTheNotFoundError()
    {
        Assert.Equal(RingoErrors.NoGame, RingoErrors.From(RingoOutcome.NoGame));
    }

    [Fact]
    public void AnAcceptedOutcome_HasNoError_WhetherOrNotItChangedTheGame()
    {
        var game = new RingoGame();

        Assert.Null(RingoErrors.From(RingoOutcome.Done(game)));
        Assert.Null(RingoErrors.From(RingoOutcome.Unchanged(game)));
    }
}
