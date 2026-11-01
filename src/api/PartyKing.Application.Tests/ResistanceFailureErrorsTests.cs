using ErrorOr;
using PartyKing.Application.Games.Services.Resistance;
using PartyKing.Domain.Entities.Games;

namespace PartyKing.Application.Tests;

/// <summary>
/// The golden table by failure: the error (type, code and message) each <see cref="ResistanceFailure"/> becomes. The
/// UI and the tests match on the codes. A failure joins the table when the action it belongs to has moved into the
/// Domain, because its old status, which <see cref="ResistanceErrorsTests"/> pinned, is gone by then.
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
    };

    [Fact]
    public void EachFailureOfTheTable_BecomesItsError()
    {
        var game = new ResistanceGame();

        foreach (var (failure, expected) in Golden)
        {
            var error = ResistanceErrors.From(ResistanceOutcome.Failed(failure, game));

            Assert.True(error.HasValue, $"{failure} should be an error");
            Assert.Equal(expected.Type, error.Value.Type);
            Assert.Equal(expected.Code, error.Value.Code);
            Assert.Equal(expected.Description, error.Value.Description);
        }
    }
}
