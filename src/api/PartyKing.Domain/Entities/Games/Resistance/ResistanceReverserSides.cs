namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>
/// Which sides get a Reverser when the Reverser module is played (the rules allow one or two). Stored in the rules JSON
/// by number, so only append; <see cref="Both"/> is 0 because it is the default.
/// </summary>
public enum ResistanceReverserSides
{
    /// <summary>One Reverser among the resistance and one among the spies.</summary>
    Both = 0,

    /// <summary>Only a Reverser among the resistance, who can fix a failed mission.</summary>
    Resistance = 1,

    /// <summary>Only a Reverser among the spies, who can turn a success into a failure.</summary>
    Spy = 2,
}
