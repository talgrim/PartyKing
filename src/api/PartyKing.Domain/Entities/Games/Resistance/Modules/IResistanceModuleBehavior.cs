namespace PartyKing.Domain.Entities.Games.Resistance.Modules;

/// <summary>
/// What one rules module is: how it is switched in <see cref="ResistanceRules"/>, what switching it too late answers
/// with, which modules it does not combine with, and what else changes in the rules when it is switched. One class per
/// module, registered in <see cref="ResistanceModules"/>; the game switches every module through it
/// (<see cref="ResistanceGame"/> does not know a single module by name).
/// </summary>
internal interface IResistanceModuleBehavior
{
    ResistanceModule Module { get; }

    /// <summary>What switching the module answers with once the game started.</summary>
    ResistanceFailure LockedFailure { get; }

    /// <summary>Whether the module is on in <paramref name="rules"/>.</summary>
    bool IsOn(ResistanceRules rules);

    /// <summary>Sets the module's switch in <paramref name="rules"/>, and nothing else (see <see cref="AfterSwitch"/>).</summary>
    void Switch(ResistanceRules rules, bool on);

    /// <summary>
    /// The modules this one does not combine with. A conflict is declared by one of the two modules only and holds both
    /// ways.
    /// </summary>
    IReadOnlyList<ResistanceModuleConflict> Conflicts => [];

    /// <summary>
    /// What else the rules say once the module is switched, e.g. the settings that only mean something while it is on.
    /// Runs right after <see cref="Switch"/>, on the same copy of the rules, wherever the module is switched from.
    /// </summary>
    void AfterSwitch(ResistanceRules rules, bool on)
    {
    }
}
