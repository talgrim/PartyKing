namespace PartyKing.Domain.Entities.Games.Resistance;

// The Blind Spies variant: see ResistanceKnowledge.KnowsSpy for what it changes.
public partial class ResistanceGame
{
    /// <summary>Switches the Blind Spies variant on or off in the rules. Allowed until the game starts, open or not.</summary>
    public ResistanceOutcome SetBlindSpies(bool enabled)
    {
        if (IsStarted)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.BlindSpiesLocked, this);
        }

        if (enabled && Rules.AssassinEnabled)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.AssassinConflict, this);
        }

        ConfigureRules(x => x.BlindSpiesEnabled = enabled);

        return Succeeded();
    }
}
