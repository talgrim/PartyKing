namespace PartyKing.Domain.Entities.Games.Resistance.Modules;

/// <summary>The Blind Spies variant: the spies do not get to know each other.</summary>
internal sealed class BlindSpiesModuleBehavior : IKnowledgeBehavior
{
    public ResistanceModule Module => ResistanceModule.BlindSpies;

    public ResistanceFailure LockedFailure => ResistanceFailure.BlindSpiesLocked;

    public bool IsOn(ResistanceRules rules)
    {
        return rules.BlindSpiesEnabled;
    }

    public void Switch(ResistanceRules rules, bool on)
    {
        rules.BlindSpiesEnabled = on;
    }

    /// <summary>With the variant on the spies do not get to know each other, and nobody else knows any spy either.</summary>
    public bool? DecidesKnowsSpy(ResistanceGame game, ResistancePlayer viewer, ResistancePlayer target)
    {
        return game.Rules.BlindSpiesEnabled ? false : null;
    }
}
