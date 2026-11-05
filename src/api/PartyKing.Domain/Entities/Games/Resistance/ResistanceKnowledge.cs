namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>
/// What a player is allowed to know about another player's secret. Every view that shows a player something about
/// somebody else asks here, so a module that changes who knows whom (Blind Spies today; the Commander, the Defector
/// and the Chiefs later) changes one place, and a leak is one test table away.
/// </summary>
public static class ResistanceKnowledge
{
    /// <summary>
    /// The side the player is on right now, which is what an identity card, an Inquisitor examination or a spy's
    /// knowledge shows: the allegiance, never the character. <see cref="ResistancePlayer.Role"/> holds it; a module that
    /// changes the allegiance during the game (the Defector) changes it there. Null until the game is started.
    /// </summary>
    public static ResistanceRole? Loyalty(ResistancePlayer player)
    {
        return player.Role;
    }

    /// <summary>
    /// Whether the viewer knows the target to be a spy: the spies know each other at the start of the game, unless the
    /// Blind Spies variant is played, or one of them is a Reverser who keeps their eyes closed (see
    /// <see cref="IsHiddenReverserSpy"/>). Nobody learns from this about their own role; the viewer has it on their card.
    /// </summary>
    public static bool KnowsSpy(ResistanceGame game, ResistancePlayer viewer, ResistancePlayer target)
    {
        if (game.Rules.BlindSpiesEnabled || viewer.PlayerId == target.PlayerId)
        {
            return false;
        }

        if (IsHiddenReverserSpy(game, viewer) || IsHiddenReverserSpy(game, target))
        {
            return false;
        }

        return Loyalty(viewer) == ResistanceRole.Spy && Loyalty(target) == ResistanceRole.Spy;
    }

    /// <summary>
    /// Whether the player is a spy Reverser who does not take part in the spies' reveal because the rules say so
    /// (<see cref="ResistanceRules.ReverserSpyKnowsSpies"/> off): they know no spy and no spy knows them, as at the
    /// table where they keep their eyes closed.
    /// </summary>
    public static bool IsHiddenReverserSpy(ResistanceGame game, ResistancePlayer player)
    {
        return game.Rules is { ReverserEnabled: true, ReverserSpyKnowsSpies: false }
               && player.Character == ResistanceCharacter.Reverser
               && Loyalty(player) == ResistanceRole.Spy;
    }
}
