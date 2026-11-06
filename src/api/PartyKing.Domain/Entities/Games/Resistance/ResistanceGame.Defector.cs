namespace PartyKing.Domain.Entities.Games.Resistance;

// The Defector module: see ResistanceDealer.Deal for the dealing, ResistanceFlow.BeginMission for the flips and
// ResistanceKnowledge.KnowsSpy for who knows whom.
public partial class ResistanceGame
{
    /// <summary>The mission at whose start the first switch card is flipped (then one at each later mission).</summary>
    public const int DefectorFirstFlipMission = 3;

    /// <summary>The cards in the switch deck: three No Change and two Change Allegiance.</summary>
    internal const int DefectorDeckSize = 5;

    /// <summary>A No Change card in <see cref="DefectorDeck"/>.</summary>
    internal const char DefectorNoChange = 'N';

    /// <summary>A Change Allegiance card in <see cref="DefectorDeck"/>.</summary>
    internal const char DefectorChangeAllegiance = 'C';

    /// <summary>
    /// The shuffled switch deck as one character per card, top first: <see cref="DefectorNoChange"/> or
    /// <see cref="DefectorChangeAllegiance"/>. Secret until a card is flipped, so only <see cref="DefectorFlips"/> leaves the Domain. Empty unless
    /// the Defector module is played in a started game.
    /// </summary>
    public string DefectorDeck { get; internal set; } = string.Empty;

    /// <summary>
    /// The switch cards flipped so far, in order, true for Change Allegiance. Public: one card is flipped at the start
    /// of each mission from <see cref="DefectorFirstFlipMission"/> on. The flips follow from the mission number, which
    /// only advances together with the flip.
    /// </summary>
    public IReadOnlyList<bool> DefectorFlips => DefectorDeck
        .Take(Math.Max(0, MissionNumber - DefectorFirstFlipMission + 1))
        .Select(x => x == DefectorChangeAllegiance)
        .ToList();

    /// <summary>Whether the Defectors are on the opposite side to the one they started on.</summary>
    internal bool DefectorsSwitched => DefectorFlips.Count(x => x) % 2 == 1;

    /// <summary>
    /// Switches the Defector module on or off in the rules and, when <paramref name="defectorsKnowEachOther"/> is given,
    /// whether the two Defectors know each other (otherwise the choice stays). Allowed until the game starts, open or
    /// not.
    /// </summary>
    public ResistanceOutcome SetDefector(bool enabled, bool? defectorsKnowEachOther = null)
    {
        if (IsStarted)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.DefectorLocked, this);
        }

        if (enabled && Rules.AssassinEnabled)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.AssassinConflict, this);
        }

        // Rules is a JSON column compared by reference, so assign a copy to have the change detected.
        Rules = Rules.Clone(defectorEnabled: enabled, defectorsKnowEachOther: defectorsKnowEachOther);

        return Succeeded();
    }
}
