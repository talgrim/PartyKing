namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>
/// What a player is allowed to know about another player's secret. Every view that shows a player something about
/// somebody else asks here, so a module that changes who knows whom (Blind Spies, Reverser and Defector today; the
/// Commander and the Chiefs later) changes one place, and a leak is one test table away.
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

        // The Commander is on the resistance side but knows every spy, except Deep Cover, who hides from them
        if (viewer.Character == ResistanceCharacter.Commander)
        {
            return Loyalty(target) == ResistanceRole.Spy && target.Character != ResistanceCharacter.DeepCover;
        }

        // The Blind Spy keeps their eyes closed in the reveal: they know no spy and no spy knows them
        if (viewer.Character == ResistanceCharacter.BlindSpy || target.Character == ResistanceCharacter.BlindSpy)
        {
            return false;
        }

        if (IsHiddenReverserSpy(game, viewer) || IsHiddenReverserSpy(game, target))
        {
            return false;
        }

        if (viewer.Character == ResistanceCharacter.Defector || IsUnknownDefector(game, target))
        {
            return false;
        }

        return Loyalty(viewer) == ResistanceRole.Spy && Loyalty(target) == ResistanceRole.Spy;
    }

    /// <summary>
    /// Whether the Body Guard (the viewer) takes the target for the Commander: the Commander and the False Commander look
    /// alike to them, so the Body Guard only learns that the Commander is one of the players this returns true for.
    /// </summary>
    public static bool KnowsCommander(ResistanceGame game, ResistancePlayer viewer, ResistancePlayer target)
    {
        return game.Rules.AssassinEnabled
               && viewer.Character == ResistanceCharacter.BodyGuard
               && target.Character is ResistanceCharacter.Commander or ResistanceCharacter.FalseCommander;
    }

    /// <summary>
    /// Whether the viewer knows the target to be the other Defector: only with the variant on
    /// (<see cref="ResistanceRules.DefectorsKnowEachOther"/>), and then whatever side each is on at the moment.
    /// </summary>
    public static bool KnowsDefector(ResistanceGame game, ResistancePlayer viewer, ResistancePlayer target)
    {
        return game.Rules is { DefectorEnabled: true, DefectorsKnowEachOther: true }
               && viewer.PlayerId != target.PlayerId
               && viewer.Character == ResistanceCharacter.Defector
               && target.Character == ResistanceCharacter.Defector;
    }

    /// <summary>
    /// Whether the player is the Defector who started on the resistance side. At the table the spies' reveal never
    /// includes them, so the spies do not know them even while a switch card has put them among the spies. The Defector
    /// who started as a spy shows a thumb in the reveal: the spies know them while they are spies, but they keep their
    /// eyes closed and know no spy (see <see cref="KnowsSpy"/>).
    /// </summary>
    public static bool IsUnknownDefector(ResistanceGame game, ResistancePlayer player)
    {
        return player.Character == ResistanceCharacter.Defector
               && (Loyalty(player) == ResistanceRole.Spy) == game.DefectorsSwitched;
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
