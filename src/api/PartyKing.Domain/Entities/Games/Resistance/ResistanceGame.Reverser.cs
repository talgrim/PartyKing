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
        if (IsStarted)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.ReverserLocked, this);
        }

        if (enabled && Rules.AssassinEnabled)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.AssassinConflict, this);
        }

        ConfigureRules(x =>
        {
            x.ReverserEnabled = enabled;
            x.ReverserSides = sides ?? x.ReverserSides;
            x.ReverserSpyKnowsSpies = spyKnowsSpies ?? x.ReverserSpyKnowsSpies;
        });

        return Succeeded();
    }

    /// <summary>
    /// The failure of a Reverser-related card play, or null when the card is allowed: Reverse only from a Reverser, and
    /// a Reverser who is a spy never plays Fail (they play Success or Reverse).
    /// </summary>
    private static ResistanceFailure? ReverserCardFailure(ResistancePlayer player, ResistanceMissionCardKind card)
    {
        return card switch
        {
            ResistanceMissionCardKind.Reverse when player.Character != ResistanceCharacter.Reverser =>
                ResistanceFailure.NotReverser,
            ResistanceMissionCardKind.Fail when player.Character == ResistanceCharacter.Reverser =>
                ResistanceFailure.ReverserCannotFail,
            _ => null,
        };
    }
}
