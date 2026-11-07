namespace PartyKing.Domain.Entities.Games.Resistance.Modules;

/// <summary>
/// A module that hooks into the flow of the game: something that happens as a mission begins, or a phase of its own
/// after a mission. <see cref="ResistanceFlow"/> asks the modules that are on (see <see cref="IResistanceModuleBehavior.IsOn"/>;
/// a module that is off is never asked), in the order of <see cref="ResistanceModules.Registered"/>. Every hook is
/// optional.
/// </summary>
/// <remarks>
/// A hook decides only what its module adds. The rest of the flow (the vote, the plot card windows, the win checks)
/// stays in <see cref="ResistanceFlow"/>, which says where each hook is asked and what comes before and after it.
/// None of the hooks draws a random number or raises an event: the flow of a game is not random.
/// </remarks>
internal interface IFlowBehavior : IResistanceModuleBehavior
{
    /// <summary>
    /// A mission begins: the phase is already <see cref="ResistancePhase.SelectingTeam"/>, the mission number and the
    /// leader are the new ones, and the plot card window or the draw has not come yet. A rejected team does not begin a
    /// mission, so it does not come here.
    /// </summary>
    void OnMissionBegun(ResistanceGame game)
    {
    }

    /// <summary>
    /// The result of a mission is recorded and announced, and the flow is about to check whether the game is over: a
    /// phase here is the next one, instead of the end (and of the plot card windows and everything else that follows a
    /// mission), or null when the module has nothing to say. The first module in the order that names one decides.
    /// </summary>
    ResistancePhase? PhaseAfterScoring(ResistanceGame game)
    {
        return null;
    }

    /// <summary>
    /// A mission that did not end the game is over and its plot card windows are closed (it is also asked after the peek
    /// window): a phase here comes before the "Dalej" step (<see cref="ResistancePhase.MissionResult"/>), or null for
    /// that step itself. The first module in the order that names one decides.
    /// </summary>
    ResistancePhase? PhaseAfterMissionResolved(ResistanceGame game)
    {
        return null;
    }
}
