namespace PartyKing.Domain.Enums;

/// <summary>What <c>Session.ScheduleDeletion</c> decided.</summary>
public enum SessionDeletionScheduleOutcome
{
    /// <summary>The session had no scheduled deletion and has one now.</summary>
    Scheduled,

    /// <summary>A deletion was already scheduled; the first time is kept and nothing changed.</summary>
    AlreadyScheduled
}
