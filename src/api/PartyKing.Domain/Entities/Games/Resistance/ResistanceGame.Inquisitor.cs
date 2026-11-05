using PartyKing.Domain.Events.Games.Resistance;

namespace PartyKing.Domain.Entities.Games.Resistance;

// The Inquisitor module (Hostile Intent): see ResistanceRoles.AfterMissionResolved for when it happens.
public partial class ResistanceGame
{
    /// <summary>
    /// Inquisitor module: the player holding the token, who examines after the next mission 2, 3 or 4 (public); null
    /// when the module is off or the game is not started.
    /// </summary>
    public Guid? InquisitorPlayerId { get; internal set; }

    /// <summary>
    /// While <see cref="ResistancePhase.Inquisition"/>: the player who picked someone and has yet to confirm they saw
    /// the loyalty (the token already moved on to the examined player); null until they pick.
    /// </summary>
    public Guid? InquisitionExaminerPlayerId { get; internal set; }

    /// <summary>The player examined in the current Inquisition once picked (the new holder of the token), else null.</summary>
    public Guid? InquisitionExaminedPlayerId => InquisitionExaminerPlayerId is null ? null : InquisitorPlayerId;

    /// <summary>Switches the Inquisitor module on or off in the rules. Allowed until the game starts, open or not.</summary>
    public ResistanceOutcome SetInquisitor(bool enabled)
    {
        if (IsStarted)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.InquisitorLocked, this);
        }

        // Rules is a JSON column compared by reference, so assign a copy to have the change detected.
        Rules = Rules.Clone(inquisitorEnabled: enabled);

        return Succeeded();
    }

    /// <summary>
    /// The holder of the token picks a player who never held it. The token passes to that player at once (publicly);
    /// the holder then privately sees the loyalty (see <see cref="ConfirmInquisition"/>).
    /// </summary>
    public ResistanceOutcome Examine(Guid? callerPlayerId, Guid targetPlayerId)
    {
        if (Inactive() is { } inactive)
        {
            return inactive;
        }

        if (Phase != ResistancePhase.Inquisition)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotInquisition, this);
        }

        var examiner = InquisitionExaminerPlayerId ?? InquisitorPlayerId;
        if (callerPlayerId is null || callerPlayerId != examiner)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotInquisitor, this);
        }

        if (InquisitionExaminerPlayerId is not null)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.AlreadyExamined, this);
        }

        if (!ResistanceRoles.CanExamine(this, callerPlayerId.Value, targetPlayerId))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.InvalidExamineTarget, this);
        }

        Players.First(x => x.PlayerId == targetPlayerId).WasInquisitor = true;
        InquisitionExaminerPlayerId = callerPlayerId;
        InquisitorPlayerId = targetPlayerId;
        Raise(new ResistanceInquisitorTokenPassed(SessionId, callerPlayerId.Value, targetPlayerId));

        return Succeeded();
    }

    /// <summary>The examiner confirms they saw the loyalty, which ends the Inquisition and shows the "Dalej" step.</summary>
    public ResistanceOutcome ConfirmInquisition(Guid? callerPlayerId)
    {
        if (Inactive() is { } inactive)
        {
            return inactive;
        }

        if (Phase != ResistancePhase.Inquisition)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotInquisition, this);
        }

        var examiner = InquisitionExaminerPlayerId ?? InquisitorPlayerId;
        if (callerPlayerId is null || callerPlayerId != examiner)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotInquisitor, this);
        }

        if (InquisitionExaminerPlayerId is null)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotExamined, this);
        }

        InquisitionExaminerPlayerId = null;
        Phase = ResistancePhase.MissionResult;

        return Succeeded();
    }
}
