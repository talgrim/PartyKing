namespace PartyKing.Domain.Entities.Games.Resistance;

// The Defector module: see ResistanceDealer.Deal for the dealing, DefectorModuleBehavior.OnMissionBegun for the flips and
// ResistanceKnowledge.KnowsSpy for who knows whom.
public partial class ResistanceGame
{
    /// <summary>Defector module: the secret switch deck. A column of the game, never null; only <see cref="DefectorFlips"/> leaves the Domain.</summary>
    public ResistanceDefectorDeck DefectorDeck { get; internal set; } = new();

    /// <summary>The switch cards flipped so far, in order, true for Change Allegiance. Public.</summary>
    public IReadOnlyList<bool> DefectorFlips => DefectorDeck.Flips(MissionNumber);

    /// <summary>Whether the Defectors are on the opposite side to the one they started on.</summary>
    internal bool DefectorsSwitched => DefectorDeck.Switched(MissionNumber);

    /// <summary>
    /// Switches the Defector module on or off in the rules and, when <paramref name="defectorsKnowEachOther"/> is given,
    /// whether the two Defectors know each other (otherwise the choice stays). Allowed until the game starts, open or
    /// not.
    /// </summary>
    public ResistanceOutcome SetDefector(bool enabled, bool? defectorsKnowEachOther = null)
    {
        return SwitchModule(
            ResistanceModule.Defector,
            enabled,
            x => x.DefectorsKnowEachOther = defectorsKnowEachOther ?? x.DefectorsKnowEachOther);
    }
}
