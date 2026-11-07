using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>
/// The switch deck of the Defector module (Hidden Agenda), one character per card, top first. Secret until a card is
/// flipped, so <see cref="Cards"/> is internal and only <see cref="ResistanceGame.DefectorFlips"/> leaves the Domain.
/// Stored in a column of the game (a complex property), never null; empty unless the Defector module is played in a
/// started game. See <c>DefectorModuleBehavior</c> for the flips.
/// </summary>
public class ResistanceDefectorDeck
{
    /// <summary>The mission at whose start the first switch card is flipped (then one at each later mission).</summary>
    public const int FirstFlipMission = 3;

    /// <summary>The cards in the switch deck: three No Change and two Change Allegiance.</summary>
    internal const int Size = 5;

    /// <summary>A No Change card.</summary>
    internal const char NoChange = 'N';

    /// <summary>A Change Allegiance card.</summary>
    internal const char ChangeAllegiance = 'C';

    /// <summary>The cards as one character per card: <see cref="NoChange"/> or <see cref="ChangeAllegiance"/>.</summary>
    internal string Cards { get; set; } = string.Empty;

    /// <summary>
    /// The cards flipped when the game is at <paramref name="missionNumber"/>, in order, true for Change Allegiance: one
    /// card is flipped at the start of each mission from <see cref="FirstFlipMission"/> on. The flips follow from the
    /// mission number, which only advances together with the flip.
    /// </summary>
    internal IReadOnlyList<bool> Flips(int missionNumber)
    {
        return Flipped(missionNumber).Select(x => x == ChangeAllegiance).ToList();
    }

    /// <summary>Whether the card flipped as <paramref name="missionNumber"/> begins is a Change Allegiance.</summary>
    internal bool ChangesAllegianceAt(int missionNumber)
    {
        var flipped = Flipped(missionNumber).ToList();

        return flipped.Count > 0
               && flipped.Count == missionNumber - FirstFlipMission + 1
               && flipped[^1] == ChangeAllegiance;
    }

    /// <summary>Whether the Defectors are on the opposite side to the one they started on at <paramref name="missionNumber"/>.</summary>
    internal bool Switched(int missionNumber)
    {
        return Flipped(missionNumber).Count(x => x == ChangeAllegiance) % 2 == 1;
    }

    /// <summary>The cards flipped by <paramref name="missionNumber"/>: the one place that says which those are.</summary>
    private IEnumerable<char> Flipped(int missionNumber)
    {
        return Cards.Take(Math.Max(0, missionNumber - FirstFlipMission + 1));
    }

    /// <summary>A shuffled deck of <see cref="Size"/> cards, two of them Change Allegiance.</summary>
    internal void Deal(Random random)
    {
        var cards = Enumerable.Repeat(NoChange, Size - 2).Concat([ChangeAllegiance, ChangeAllegiance]);
        Cards = new string(cards.Shuffled(random).ToArray());
    }

    internal void Clear()
    {
        Cards = string.Empty;
    }
}
