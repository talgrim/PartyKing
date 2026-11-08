namespace PartyKing.Domain.Entities.Games.Resistance;

// The Reverser module: see ResistanceMissionRules.HasSucceeded for the result and ResistanceDealer.Deal for the dealing.
public partial class ResistanceGame
{
    /// <summary>
    /// Switches the Reverser module on or off in the rules and, when <paramref name="sides"/> is given, chooses which
    /// sides get a Reverser and <paramref name="spyKnowsSpies"/> whether a spy Reverser and the other spies know each
    /// other (otherwise the choices stay). Allowed until the game starts, open or not.
    /// </summary>
    public ResistanceOutcome SetReverser(
        bool enabled, ResistanceReverserSides? sides = null, bool? spyKnowsSpies = null)
    {
        return SwitchModule(
            ResistanceModule.Reverser,
            enabled,
            x =>
            {
                x.ReverserSides = sides ?? x.ReverserSides;
                x.ReverserSpyKnowsSpies = spyKnowsSpies ?? x.ReverserSpyKnowsSpies;
            });
    }
}
