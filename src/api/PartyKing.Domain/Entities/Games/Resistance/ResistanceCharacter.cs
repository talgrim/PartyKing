namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>
/// The special character of a player, apart from their side (<see cref="ResistanceRole"/>). Most players are
/// <see cref="Standard"/>; only the modules that deal characters give others. Only append.
/// </summary>
public enum ResistanceCharacter
{
    Standard = 0,

    /// <summary>Reverser module: may play a Reverse card. One on the resistance side and one among the spies.</summary>
    Reverser = 1,

    /// <summary>
    /// Defector module: one on each side, whose allegiance may change at the start of the missions 3 to 5. The character
    /// stays; <see cref="ResistancePlayer.Role"/> changes.
    /// </summary>
    Defector = 2,
}
