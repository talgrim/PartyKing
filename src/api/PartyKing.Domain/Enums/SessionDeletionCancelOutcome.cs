namespace PartyKing.Domain.Enums;

/// <summary>What <c>Session.CancelDeletion</c> decided.</summary>
public enum SessionDeletionCancelOutcome
{
    /// <summary>A deletion was scheduled and is not any more.</summary>
    Cancelled,

    /// <summary>No deletion was scheduled; nothing changed.</summary>
    NotScheduled
}
