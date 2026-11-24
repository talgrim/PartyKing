namespace PartyKing.Domain.Enums;

/// <summary>What <c>ISessionsRepository.ScheduleDeletionAsync</c> did.</summary>
public enum SessionDeletionScheduleResult
{
    /// <summary>The deletion is scheduled now; the scheduling event was raised.</summary>
    Scheduled,

    /// <summary>A deletion was already scheduled; the first time is kept and nothing was raised.</summary>
    AlreadyScheduled,

    SessionNotFound
}
