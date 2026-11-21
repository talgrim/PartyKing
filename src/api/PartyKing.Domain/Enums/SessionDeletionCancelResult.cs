namespace PartyKing.Domain.Enums;

/// <summary>What <c>ISessionsRepository.CancelDeletionAsync</c> did.</summary>
public enum SessionDeletionCancelResult
{
    /// <summary>The scheduled deletion was called off; the cancellation event was raised.</summary>
    Cancelled,

    /// <summary>No deletion was scheduled; nothing changed.</summary>
    NotScheduled,

    SessionNotFound
}
