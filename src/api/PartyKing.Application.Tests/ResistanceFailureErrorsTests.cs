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
        [ResistanceFailure.NotStarted] = new(ErrorType.Conflict, "Resistance.NotStarted", "The game has not started yet."),
        [ResistanceFailure.NotOpen] = new(ErrorType.Conflict, "Resistance.NotOpen", "The game is not open to guests yet."),
        [ResistanceFailure.AlreadyStarted] = new(
            ErrorType.Conflict, "Resistance.AlreadyStarted", "The game has already started."),
        [ResistanceFailure.NotEnoughPlayers] = new(
            ErrorType.Conflict, "Resistance.NotEnoughPlayers", "The game needs at least 5 players to start."),
        [ResistanceFailure.NotOver] = new(ErrorType.Conflict, "Resistance.NotOver", "The game is still being played."),
        [ResistanceFailure.NotPlayerToVote] = new(
            ErrorType.Forbidden, "Resistance.NotPlayer", "Only the players of the game vote."),
        [ResistanceFailure.NotVoting] = new(ErrorType.Conflict, "Resistance.NotVoting", "No team is up for a vote."),
        [ResistanceFailure.VoteLocked] = new(
            ErrorType.Conflict, "Resistance.VoteLocked", "An Opinion Maker's vote can't be changed."),
        [ResistanceFailure.OpinionMakersFirst] = new(
            ErrorType.Conflict, "Resistance.OpinionMakersFirst", "The Opinion Makers vote first."),
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
