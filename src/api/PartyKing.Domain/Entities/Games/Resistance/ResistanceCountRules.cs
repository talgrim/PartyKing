namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>The rules for one player count: spies, team size and fails needed for each of the five missions.</summary>
public record ResistanceCountRules(int Spies, int[] TeamSizes, int[] RequiredFails)
{
    /// <summary>
    /// What is wrong with these rules for a game of <paramref name="playerCount"/> players; empty when they can be
    /// played. A player count outside 5-10 is the only failure reported then, and the required fails are checked only
    /// against team sizes that are valid themselves.
    /// </summary>
    public IReadOnlyList<ResistanceRulesFailure> Validate(int playerCount)
    {
        if (playerCount is < ResistanceGame.MinPlayers or > ResistanceGame.MaxPlayers)
        {
            return [ResistanceRulesFailure.PlayerCountOutOfRange];
        }

        var failures = new List<ResistanceRulesFailure>();
        if (Spies < 1 || Spies >= playerCount)
        {
            failures.Add(ResistanceRulesFailure.SpiesOutOfRange);
        }

        if (TeamSizes.Length != ResistanceRules.Missions || TeamSizes.Any(x => x < 1 || x > playerCount))
        {
            failures.Add(ResistanceRulesFailure.InvalidTeamSizes);

            return failures;
        }

        if (RequiredFails.Length != ResistanceRules.Missions
            || RequiredFails.Zip(TeamSizes).Any(x => x.First < 1 || x.First > x.Second))
        {
            failures.Add(ResistanceRulesFailure.InvalidRequiredFails);
        }

        return failures;
    }
}
