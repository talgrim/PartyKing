namespace PartyKing.Domain.Entities.Games.Resistance.Modules;

/// <summary>
/// A module that has something to do when the game starts or restarts: deal its cards or token, check that the game
/// can be started with it, clear what it accumulated (a module that deals characters is an
/// <see cref="ICharacterDealBehavior"/>). <see cref="ResistanceDealer"/> and
/// <see cref="ResistanceGame.Start"/> ask every module that implements this, in the order of
/// <see cref="ResistanceModules.Registered"/>.
/// </summary>
/// <remarks>
/// <see cref="Reset"/> is the one place that clears what a module accumulated: the dealing methods start from a clear
/// state (a new game, or one that was restarted, which resets), and do not clear it again.
/// </remarks>
internal interface IDealBehavior : IResistanceModuleBehavior
{
    /// <summary>
    /// Why the game cannot be started with the module on for these players (the count, the rules), or null. The first
    /// module in the order that fails answers.
    /// </summary>
    ResistanceFailure? StartFailure(ResistanceGame game)
    {
        return null;
    }

    /// <summary>Deals what depends on the first leader (a token, a deck). Runs after the leader is picked.</summary>
    void DealTable(ResistanceGame game, Random random)
    {
    }

    /// <summary>
    /// Clears what the module accumulated, whether it is on or not, when the game is restarted. The only place that does.
    /// </summary>
    void Reset(ResistanceGame game)
    {
    }
}
