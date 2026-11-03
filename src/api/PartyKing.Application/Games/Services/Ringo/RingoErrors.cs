using ErrorOr;
using PartyKing.Domain.Entities.Games.Ringo;

namespace PartyKing.Application.Games.Services.Ringo;

/// <summary>The error each <see cref="RingoFailure"/> becomes. Codes are matched by the UI.</summary>
internal static class RingoErrors
{
    public const string GameName = "Ringo";

    public static Error NoGame => Error.NotFound($"{GameName}.NotFound", $"The session has no {GameName} game.");

    /// <summary>The error of an action's outcome, or null when the action was accepted.</summary>
    public static Error? From(RingoOutcome outcome) => outcome.Failure switch
    {
        null => null,
        RingoFailure.NoGame => NoGame,
        RingoFailure.NotYourTurn => Error.Conflict("Ringo.NotYourTurn", "It is another player's turn."),
        RingoFailure.GameOver => Error.Conflict("Ringo.GameOver", "The fourth king ended the game."),
        RingoFailure.CardPending => Error.Conflict(
            "Ringo.CardPending", "Continue past the flipped card before flipping another."),
        var failure => throw new ArgumentOutOfRangeException(
            nameof(outcome), failure, "No error is defined for this failure."),
    };
}
