namespace PartyKing.Domain.Entities.Games;

/// <summary>
/// The public outcome of one finished mission: only the number of Fail cards, never who played them. Keyed by
/// <see cref="SessionId"/> and <see cref="MissionNumber"/>.
/// </summary>
public class ResistanceMissionResult
{
    public Guid SessionId { get; set; }

    /// <summary>1 to 5.</summary>
    public int MissionNumber { get; set; }

    /// <summary>The Fail cards played; the mission failed when they reached <see cref="ResistanceRules.RequiredFails"/>.</summary>
    public int FailCount { get; set; }

    public bool Succeeded { get; set; }
}
