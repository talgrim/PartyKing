namespace PartyKing.Domain.Entities.Games.Resistance.Modules;

/// <summary>
/// The Assassin module (Hidden Agenda). It takes the place of what the Defector, the Reverser and Blind Spies change
/// about who knows whom, so it does not combine with them.
/// </summary>
internal sealed class AssassinModuleBehavior : IKnowledgeBehavior, ICharacterDealBehavior, IDealBehavior
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

    /// <summary>
    /// The failure of the optional characters for the players of this game, or null when they fit: the Assassin takes
    /// one spy seat, the other spy characters share the rest, and with 5 players a Body Guard needs Deep Cover or False
    /// Commander (or the Commander, the Body Guard and the spies' side would give the game away).
    /// </summary>
    public ResistanceFailure? StartFailure(ResistanceGame game)
    {
        var rules = game.Rules;
        if (!rules.AssassinEnabled)
        {
            return null;
        }

        if (rules.OptionalSpyCharacters > rules.SpyCount(game.Players.Count) - 1)
        {
            return ResistanceFailure.TooManySpyCharacters;
        }

        if (rules.BodyGuardEnabled && game.Players.Count == BodyGuardCoverPlayerCount
                                   && !rules.DeepCoverEnabled && !rules.FalseCommanderEnabled)
        {
            return ResistanceFailure.BodyGuardNeedsCover;
        }

        return null;
    }

    /// <summary>The player count at which a Body Guard needs Deep Cover or False Commander next to them.</summary>
    internal const int BodyGuardCoverPlayerCount = 5;

    /// <summary>
    /// The Assassin is the first standard spy and the Commander the first standard resistance member of the shuffle (no
    /// extra draw, so a game without the module deals as before). The module does not combine with the Reverser or the
    /// Defector, so every player is still a standard one here.
    /// </summary>
    public void DealCharacters(ResistanceGame game, List<ResistancePlayer> shuffled, Random random)
    {
        if (!game.Rules.AssassinEnabled)
        {
            return;
        }

        shuffled.First(x => x.Role == ResistanceRole.Spy && x.Character == ResistanceCharacter.Standard)
            .Character = ResistanceCharacter.Assassin;
        shuffled.First(x => x.Role == ResistanceRole.Resistance && x.Character == ResistanceCharacter.Standard)
            .Character = ResistanceCharacter.Commander;

        // The optional characters take the next standard players of their side; the start already checked they fit
        var optional = new (bool Enabled, ResistanceRole Side, ResistanceCharacter Character)[]
        {
            (game.Rules.BodyGuardEnabled, ResistanceRole.Resistance, ResistanceCharacter.BodyGuard),
            (game.Rules.DeepCoverEnabled, ResistanceRole.Spy, ResistanceCharacter.DeepCover),
            (game.Rules.BlindSpyEnabled, ResistanceRole.Spy, ResistanceCharacter.BlindSpy),
            (game.Rules.FalseCommanderEnabled, ResistanceRole.Spy, ResistanceCharacter.FalseCommander),
        };
        foreach (var (enabled, side, character) in optional.Where(x => x.Enabled))
        {
            shuffled.First(x => x.Role == side && x.Character == ResistanceCharacter.Standard).Character = character;
        }
    }

    public void Reset(ResistanceGame game)
    {
        game.AssassinationTargetPlayerId = null;
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
