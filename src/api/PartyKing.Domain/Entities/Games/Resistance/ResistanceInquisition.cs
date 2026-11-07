namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>
/// The state of the Inquisitor module (Hostile Intent): who holds the token and, during an Inquisition, who picked
/// someone. Stored in the columns of the game (a complex property), never null; see
/// <see cref="ResistanceGame.Examine"/> and <see cref="ResistanceGame.ConfirmInquisition"/> for the steps.
/// </summary>
public class ResistanceInquisition
{
    // The setters are private on purpose: ExaminedPlayerId and ActingPlayerId follow from the two ids, so they only
    // change together, through Hold, Pick, Finish and Clear (EF sets them by reflection).

    /// <summary>
    /// The player holding the token, who examines after the next mission 2, 3 or 4 (public); null when the module is off
    /// or the game is not started.
    /// </summary>
    public Guid? HolderPlayerId { get; private set; }

    /// <summary>
    /// While <see cref="ResistancePhase.Inquisition"/>: the player who picked someone and has yet to confirm they saw
    /// the loyalty (the token already moved on to the examined player); null until they pick.
    /// </summary>
    public Guid? ExaminerPlayerId { get; private set; }

    /// <summary>Whether the examiner picked someone in the current Inquisition.</summary>
    public bool HasPicked => ExaminerPlayerId is not null;

    /// <summary>The player examined in the current Inquisition once picked (the new holder of the token), else null.</summary>
    public Guid? ExaminedPlayerId => HasPicked ? HolderPlayerId : null;

    /// <summary>
    /// The player who has to act in the current Inquisition: the holder until they pick, then the examiner, who
    /// confirms they saw the loyalty (the token already moved on).
    /// </summary>
    public Guid? ActingPlayerId => ExaminerPlayerId ?? HolderPlayerId;

    /// <summary>Gives the token to a player, at the start of the game, with no Inquisition going on.</summary>
    internal void Hold(Guid playerId)
    {
        HolderPlayerId = playerId;
        ExaminerPlayerId = null;
    }

    /// <summary>The examiner picks a player: the token passes to them at once and the examiner is remembered.</summary>
    internal void Pick(Guid examinerPlayerId, Guid examinedPlayerId)
    {
        ExaminerPlayerId = examinerPlayerId;
        HolderPlayerId = examinedPlayerId;
    }

    /// <summary>The examiner confirmed they saw the loyalty; the token stays with the examined player.</summary>
    internal void Finish()
    {
        ExaminerPlayerId = null;
    }

    /// <summary>No token and no Inquisition: the module is off or the game is not started.</summary>
    internal void Clear()
    {
        HolderPlayerId = null;
        ExaminerPlayerId = null;
    }
}
