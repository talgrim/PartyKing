using PartyKing.Domain.Entities.Games.Resistance.Modules;

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
/// The registry of the rules modules: one <see cref="IResistanceModuleBehavior"/> per <see cref="ResistanceModule"/>,
/// which <see cref="ResistanceGame"/> switches the modules by. A new module is a new behavior class in
/// <c>Modules/</c> and one line in <see cref="Registered"/>; it declares which modules it does not combine with, so no
/// existing module changes.
/// </summary>
internal static class ResistanceModules
{
    /// <summary>
    /// The modules in the order they are checked in, which is the order of <see cref="ResistanceModule"/> (a test pins
    /// it). The order decides which failure a switch answers with when more than one conflict applies at once.
    /// </summary>
    public static IReadOnlyList<IResistanceModuleBehavior> Registered { get; } =
    [
        new PlotCardsModuleBehavior(),
        new InquisitorModuleBehavior(),
        new BlindSpiesModuleBehavior(),
        new ReverserModuleBehavior(),
        new DefectorModuleBehavior(),
        new AssassinModuleBehavior(),
    ];

    private static readonly IReadOnlyDictionary<ResistanceModule, IResistanceModuleBehavior> Behaviors =
        Registered.ToDictionary(x => x.Module);

    /// <summary>The modules that change who knows which players are spies, in the order of <see cref="Registered"/>.</summary>
    public static IReadOnlyList<IKnowledgeBehavior> Knowledge { get; } = Registered.OfType<IKnowledgeBehavior>().ToList();

    public static IResistanceModuleBehavior For(ResistanceModule module)
    {
        return Behaviors.TryGetValue(module, out var behavior)
            ? behavior
            : throw new InvalidOperationException($"The module {module} has no behavior registered.");
    }

    /// <summary>What switching the module answers with once the game started.</summary>
    public static ResistanceFailure LockedFailure(ResistanceModule module) => For(module).LockedFailure;

    /// <summary>Switches the module in <paramref name="rules"/>, together with what the module's rules say follows from it.</summary>
    public static void Switch(ResistanceRules rules, ResistanceModule module, bool on)
    {
        var behavior = For(module);
        behavior.Switch(rules, on);
        behavior.AfterSwitch(rules, on);
    }

    /// <summary>
    /// The failure for switching <paramref name="module"/> on while a module it does not combine with is on already, or
    /// null when nothing stands in its way. A conflict declared by either of the two modules counts.
    /// </summary>
    public static ResistanceFailure? ConflictFailure(ResistanceRules rules, ResistanceModule module)
    {
        return ConflictFailure(Registered, rules, module);
    }

    /// <summary>
    /// The same over the given modules. When more than one conflict applies the answer is the first one in the order
    /// the modules are given in, and within a module in the order it declares them.
    /// </summary>
    internal static ResistanceFailure? ConflictFailure(
        IReadOnlyList<IResistanceModuleBehavior> modules, ResistanceRules rules, ResistanceModule module)
    {
        foreach (var declaring in modules)
        {
            foreach (var conflict in declaring.Conflicts)
            {
                var declaredByThis = declaring.Module == module && IsOn(modules, rules, conflict.With);
                var declaredByTheOther = conflict.With == module && declaring.IsOn(rules);

                if (declaredByThis || declaredByTheOther)
                {
                    return conflict.Failure;
                }
            }
        }

        return null;
    }

    private static bool IsOn(IReadOnlyList<IResistanceModuleBehavior> modules, ResistanceRules rules, ResistanceModule module)
    {
        return modules.Single(x => x.Module == module).IsOn(rules);
    }
}
