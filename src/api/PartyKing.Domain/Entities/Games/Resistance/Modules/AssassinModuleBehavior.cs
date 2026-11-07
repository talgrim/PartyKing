namespace PartyKing.Domain.Entities.Games.Resistance.Modules;

/// <summary>
/// The Assassin module (Hidden Agenda). It takes the place of what the Defector, the Reverser and Blind Spies change
/// about who knows whom, so it does not combine with them.
/// </summary>
internal sealed class AssassinModuleBehavior : IKnowledgeBehavior
{
    public ResistanceModule Module => ResistanceModule.Assassin;

    public ResistanceFailure LockedFailure => ResistanceFailure.AssassinLocked;

    public IReadOnlyList<ResistanceModuleConflict> Conflicts { get; } =
    [
        new(ResistanceModule.Defector, ResistanceFailure.AssassinConflict),
        new(ResistanceModule.Reverser, ResistanceFailure.AssassinConflict),
        new(ResistanceModule.BlindSpies, ResistanceFailure.AssassinConflict),
    ];

    public bool IsOn(ResistanceRules rules)
    {
        return rules.AssassinEnabled;
    }

    public void Switch(ResistanceRules rules, bool on)
    {
        rules.AssassinEnabled = on;
    }

    public bool SeesSpies(ResistancePlayer viewer)
    {
        return viewer.Character == ResistanceCharacter.Commander;
    }

    /// <summary>The Commander is on the resistance side but knows every spy, except Deep Cover, who hides from them.</summary>
    public bool? DecidesKnowsSpy(ResistanceGame game, ResistancePlayer viewer, ResistancePlayer target)
    {
        if (viewer.Character != ResistanceCharacter.Commander)
        {
            return null;
        }

        return ResistanceKnowledge.Loyalty(target) == ResistanceRole.Spy && target.Character != ResistanceCharacter.DeepCover;
    }

    /// <summary>The Blind Spy keeps their eyes closed in the reveal: they know no spy and no spy knows them.</summary>
    public bool HidesSpyFrom(ResistanceGame game, ResistancePlayer viewer, ResistancePlayer target)
    {
        return viewer.Character == ResistanceCharacter.BlindSpy || target.Character == ResistanceCharacter.BlindSpy;
    }

    /// <summary>The optional characters mean nothing without the module, so switching it off switches them off too.</summary>
    public void AfterSwitch(ResistanceRules rules, bool on)
    {
        if (on)
        {
            return;
        }

        rules.BodyGuardEnabled = false;
        rules.DeepCoverEnabled = false;
        rules.BlindSpyEnabled = false;
        rules.FalseCommanderEnabled = false;
    }
}
