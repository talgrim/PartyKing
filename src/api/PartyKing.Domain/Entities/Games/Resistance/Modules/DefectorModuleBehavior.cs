namespace PartyKing.Domain.Entities.Games.Resistance.Modules;

/// <summary>The Defector module (Hidden Agenda).</summary>
internal sealed class DefectorModuleBehavior : IKnowledgeBehavior
{
    public ResistanceModule Module => ResistanceModule.Defector;

    public ResistanceFailure LockedFailure => ResistanceFailure.DefectorLocked;

    public bool IsOn(ResistanceRules rules)
    {
        return rules.DefectorEnabled;
    }

    public void Switch(ResistanceRules rules, bool on)
    {
        rules.DefectorEnabled = on;
    }

    /// <summary>A Defector keeps their eyes closed in the reveal, and the Defector who started as resistance is not in it.</summary>
    public bool HidesSpyFrom(ResistanceGame game, ResistancePlayer viewer, ResistancePlayer target)
    {
        return viewer.Character == ResistanceCharacter.Defector || IsUnknown(game, target);
    }

    /// <summary>
    /// Whether the player is the Defector who started on the resistance side. At the table the spies' reveal never
    /// includes them, so the spies do not know them even while a switch card has put them among the spies. The Defector
    /// who started as a spy shows a thumb in the reveal: the spies know them while they are spies, but they keep their
    /// eyes closed and know no spy.
    /// </summary>
    public static bool IsUnknown(ResistanceGame game, ResistancePlayer player)
    {
        return player.Character == ResistanceCharacter.Defector
               && (ResistanceKnowledge.Loyalty(player) == ResistanceRole.Spy) == game.DefectorsSwitched;
    }
}
