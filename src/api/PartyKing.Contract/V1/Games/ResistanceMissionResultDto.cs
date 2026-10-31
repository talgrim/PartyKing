namespace PartyKing.Contract.V1.Games;

/// <summary>
/// The public outcome of a finished mission: the team that went and how many Fail cards were played, not by whom.
/// The team is empty for a mission recorded before teams were kept.
/// </summary>
public record ResistanceMissionResultDto(int MissionNumber, bool Succeeded, int FailCount, IReadOnlyList<Guid> TeamPlayerIds);
