namespace PartyKing.Domain.Entities.Games.Resistance;

// Switching the rules modules on and off: the modules and what does not combine are in ResistanceModules.
public partial class ResistanceGame
{
    /// <summary>
    /// Switches a module on or off, and applies the module's own settings through <paramref name="settings"/>. Allowed
    /// until the game starts, open or not; switching a module on is refused while one it does not combine with is on.
    /// Switching a module off is always allowed. A refused switch changes nothing.
    /// </summary>
    private ResistanceOutcome SwitchModule(
        ResistanceModule module, bool enabled, Action<ResistanceRules>? settings = null)
    {
        if (IsStarted)
        {
            return ResistanceOutcome.Failed(ResistanceModules.LockedFailure(module), this);
        }

        if (enabled && ResistanceModules.ConflictFailure(Rules, module) is { } conflict)
        {
            return ResistanceOutcome.Failed(conflict, this);
        }

        ConfigureRules(x =>
        {
            ResistanceModules.Switch(x, module, enabled);
            settings?.Invoke(x);
        });

        return Succeeded();
    }

    /// <summary>
    /// Changes the rules through a copy. Rules is a JSON column compared by reference, so the change is only noticed
    /// (and saved) when a new instance is assigned.
    /// </summary>
    private void ConfigureRules(Action<ResistanceRules> change)
    {
        var copy = Rules.Clone();
        change(copy);
        Rules = copy;
    }
}
