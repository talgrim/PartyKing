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

    /// <summary>
    /// Assassin module: the one resistance member who knows the spies. If the resistance wins three missions, the
    /// Assassin gets one guess at who they are.
    /// </summary>
    Commander = 3,

    /// <summary>Assassin module: the spy who names the Commander after the third successful mission.</summary>
    Assassin = 4,

    /// <summary>Assassin module, optional: a resistance member who knows the Commander from the start (and the False Commander just as well).</summary>
    BodyGuard = 5,

    /// <summary>Assassin module, optional: a spy the Commander does not see.</summary>
    DeepCover = 6,

    /// <summary>Assassin module, optional: a spy who knows no other spy, and whom no other spy knows (the Commander does see them).</summary>
    BlindSpy = 7,

    /// <summary>Assassin module, optional: a spy who looks like the Commander to the Body Guard.</summary>
    FalseCommander = 8,
}
