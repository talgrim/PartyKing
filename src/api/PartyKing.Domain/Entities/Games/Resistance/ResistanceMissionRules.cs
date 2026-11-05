namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>The one place that turns the cards of a finished mission into its result.</summary>
public static class ResistanceMissionRules
{
    /// <summary>
    /// Whether the mission succeeded. Without a Reverse card it fails when the Fail cards reach
    /// <paramref name="requiredFails"/>. Every Reverse card turns that result around, so an odd number of them flips it
    /// and two cancel out. That also decides the case the rule text leaves open: on a mission that needs two fails, one
    /// Fail and one Reverse is a failure, because the single Fail alone would not have failed it.
    /// </summary>
    public static bool HasSucceeded(IReadOnlyCollection<ResistanceMissionCardKind> cards, int requiredFails)
    {
        var failedWithoutReverse = cards.Count(x => x == ResistanceMissionCardKind.Fail) >= requiredFails;
        var reversed = cards.Count(x => x == ResistanceMissionCardKind.Reverse) % 2 == 1;

        return failedWithoutReverse == reversed;
    }
}
