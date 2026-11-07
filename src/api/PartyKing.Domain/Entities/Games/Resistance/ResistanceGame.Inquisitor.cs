using PartyKing.Domain.Events.Games.Resistance;

namespace PartyKing.Domain.Entities.Games.Resistance;

// The Inquisitor module (Hostile Intent): see ResistanceFlow.AfterMissionResolved for when it happens.
public partial class ResistanceGame
{
    /// <summary>Inquisitor module: who holds the token and who is examining. A column pair of the game, never null.</summary>
    public ResistanceInquisition Inquisition { get; internal set; } = new();

    /// <summary>Switches the Inquisitor module on or off in the rules. Allowed until the game starts, open or not.</summary>
    public ResistanceOutcome SetInquisitor(bool enabled) => SwitchModule(ResistanceModule.Inquisitor, enabled);

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

        if (callerPlayerId is null || callerPlayerId != Inquisition.ActingPlayerId)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotInquisitor, this);
        }

        if (Inquisition.HasPicked)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.AlreadyExamined, this);
        }

        if (!ResistanceFlow.CanExamine(this, callerPlayerId.Value, targetPlayerId))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.InvalidExamineTarget, this);
        }

        Players.First(x => x.PlayerId == targetPlayerId).WasInquisitor = true;
        Inquisition.Pick(callerPlayerId.Value, targetPlayerId);
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

        if (callerPlayerId is null || callerPlayerId != Inquisition.ActingPlayerId)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotInquisitor, this);
        }

        if (!Inquisition.HasPicked)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotExamined, this);
        }

        Inquisition.Finish();
        ResistanceFlow.AfterInquisition(this);

        return Succeeded();
    }
}
