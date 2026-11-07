namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>A rules module that is switched on or off before the game starts (see <see cref="ResistanceModules"/>).</summary>
internal enum ResistanceModule
{
    PlotCards,
    Inquisitor,
    BlindSpies,
    Reverser,
    Defector,
    Assassin,
}

/// <summary>
/// What is known about each <see cref="ResistanceModule"/>: the single table that <see cref="ResistanceGame"/>
/// switches modules by. A new module is one row in <see cref="Descriptors"/>, which also says which modules it does
/// not combine with, so no row of an existing module changes.
/// </summary>
internal static class ResistanceModules
{
    /// <summary>A module that is not switched on together with another, and what the refused one answers with.</summary>
    private sealed record Conflict(ResistanceModule With, ResistanceFailure Failure);

    /// <summary>
    /// The switch of a module in <see cref="ResistanceRules"/>, the failure for switching it too late and the modules
    /// it does not combine with. A conflict is declared on one side only and holds both ways.
    /// </summary>
    private sealed record Descriptor(
        ResistanceFailure Locked,
        Func<ResistanceRules, bool> IsOn,
        Action<ResistanceRules, bool> Switch,
        params Conflict[] Conflicts);

    private static readonly Dictionary<ResistanceModule, Descriptor> Descriptors = new()
    {
        [ResistanceModule.PlotCards] = new(
            ResistanceFailure.PlotCardsLocked, x => x.PlotCardsEnabled, (x, on) => x.PlotCardsEnabled = on),
        [ResistanceModule.Inquisitor] = new(
            ResistanceFailure.InquisitorLocked, x => x.InquisitorEnabled, (x, on) => x.InquisitorEnabled = on),
        [ResistanceModule.BlindSpies] = new(
            ResistanceFailure.BlindSpiesLocked, x => x.BlindSpiesEnabled, (x, on) => x.BlindSpiesEnabled = on),
        [ResistanceModule.Reverser] = new(
            ResistanceFailure.ReverserLocked, x => x.ReverserEnabled, (x, on) => x.ReverserEnabled = on),
        [ResistanceModule.Defector] = new(
            ResistanceFailure.DefectorLocked, x => x.DefectorEnabled, (x, on) => x.DefectorEnabled = on),

        // The Assassin takes the place of what the other three change about who knows whom
        [ResistanceModule.Assassin] = new(
            ResistanceFailure.AssassinLocked,
            x => x.AssassinEnabled,
            (x, on) => x.AssassinEnabled = on,
            new Conflict(ResistanceModule.Defector, ResistanceFailure.AssassinConflict),
            new Conflict(ResistanceModule.Reverser, ResistanceFailure.AssassinConflict),
            new Conflict(ResistanceModule.BlindSpies, ResistanceFailure.AssassinConflict)),
    };

    /// <summary>What switching the module answers with once the game started.</summary>
    public static ResistanceFailure LockedFailure(ResistanceModule module) => Descriptors[module].Locked;

    public static void Switch(ResistanceRules rules, ResistanceModule module, bool on) =>
        Descriptors[module].Switch(rules, on);

    /// <summary>
    /// The failure for switching <paramref name="module"/> on while a module it does not combine with is on already, or
    /// null when nothing stands in its way. A conflict declared by either of the two modules counts.
    /// </summary>
    public static ResistanceFailure? ConflictFailure(ResistanceRules rules, ResistanceModule module)
    {
        foreach (var (other, descriptor) in Descriptors)
        {
            foreach (var conflict in descriptor.Conflicts)
            {
                var declaredByThis = other == module && Descriptors[conflict.With].IsOn(rules);
                var declaredByTheOther = conflict.With == module && descriptor.IsOn(rules);

                if (declaredByThis || declaredByTheOther)
                {
                    return conflict.Failure;
                }
            }
        }

        return null;
    }
}
