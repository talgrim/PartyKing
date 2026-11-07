namespace PartyKing.Domain.Entities.Games.Resistance.Modules;

/// <summary>The Reverser module.</summary>
internal sealed class ReverserModuleBehavior : IKnowledgeBehavior, IDealBehavior
{
    public ResistanceModule Module => ResistanceModule.Reverser;

    public ResistanceFailure LockedFailure => ResistanceFailure.ReverserLocked;

    public bool IsOn(ResistanceRules rules)
    {
        return rules.ReverserEnabled;
    }

    public void Switch(ResistanceRules rules, bool on)
    {
        rules.ReverserEnabled = on;
    }

    /// <summary>A Reverser on each side the rules ask for: the first standard player of the side in the shuffle.</summary>
    public void DealCharacters(ResistanceGame game, List<ResistancePlayer> shuffled, Random random)
    {
        if (!game.Rules.ReverserEnabled)
        {
            return;
        }

        // The shuffle is random, so the first of a side is a random member of it
        var sides = game.Rules.ReverserSides;
        if (sides != ResistanceReverserSides.Resistance)
        {
            shuffled.First(x => x.Role == ResistanceRole.Spy && x.Character == ResistanceCharacter.Standard)
                .Character = ResistanceCharacter.Reverser;
        }

        if (sides != ResistanceReverserSides.Spy)
        {
            shuffled.First(x => x.Role == ResistanceRole.Resistance && x.Character == ResistanceCharacter.Standard)
                .Character = ResistanceCharacter.Reverser;
        }
    }

    public bool HidesSpyFrom(ResistanceGame game, ResistancePlayer viewer, ResistancePlayer target)
    {
        return IsHiddenSpy(game, viewer) || IsHiddenSpy(game, target);
    }

    /// <summary>
    /// Whether the player is a spy Reverser who does not take part in the spies' reveal because the rules say so
    /// (<see cref="ResistanceRules.ReverserSpyKnowsSpies"/> off): they know no spy and no spy knows them, as at the
    /// table where they keep their eyes closed.
    /// </summary>
    public static bool IsHiddenSpy(ResistanceGame game, ResistancePlayer player)
    {
        return game.Rules is { ReverserEnabled: true, ReverserSpyKnowsSpies: false }
               && player.Character == ResistanceCharacter.Reverser
               && ResistanceKnowledge.Loyalty(player) == ResistanceRole.Spy;
    }
}
