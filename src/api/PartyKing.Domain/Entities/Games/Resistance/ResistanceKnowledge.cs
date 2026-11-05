namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>
/// What a player is allowed to know about another player's secret. Every view that shows a player something about
/// somebody else asks here, so a module that changes who knows whom (Blind Spies today; the Commander, the Defector
/// and the Chiefs later) changes one place, and a leak is one test table away.
/// </summary>
public static class ResistanceKnowledge
{
    /// <summary>
    /// Whether the viewer knows the target to be a spy: the spies know each other at the start of the game, unless the
    /// Blind Spies variant is played. Nobody learns from this about their own role; the viewer has it on their card.
    /// </summary>
    public static bool KnowsSpy(ResistanceGame game, ResistancePlayer viewer, ResistancePlayer target)
    {
        if (game.Rules.BlindSpiesEnabled || viewer.PlayerId == target.PlayerId)
        {
            return false;
        }

        return viewer.Role == ResistanceRole.Spy && target.Role == ResistanceRole.Spy;
    }
}
