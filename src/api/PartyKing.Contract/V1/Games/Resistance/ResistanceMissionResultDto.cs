namespace PartyKing.Contract.V1.Games.Resistance;

/// <summary>
/// The public outcome of a finished mission: the team that went and how many Fail and Reverse cards were played, not by whom.
/// The team is empty for a mission recorded before teams were kept.
/// </summary>
/// <param name="ReverseCount">The Reverse cards played (Reverser module; 0 otherwise).</param>
public record ResistanceMissionResultDto(
    int MissionNumber, bool Succeeded, int FailCount, int ReverseCount, IReadOnlyList<Guid> TeamPlayerIds);
