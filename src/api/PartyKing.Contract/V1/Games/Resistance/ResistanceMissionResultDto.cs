namespace PartyKing.Contract.V1.Games.Resistance;

/// <summary>
/// The public outcome of a finished mission: the team that went and how many Fail cards were played, not by whom.
/// The team is empty for a mission recorded before teams were kept.
/// </summary>
/// <param name="FailCount">The Fail cards played; null with the Reverser module, where the number would tell that a Reverser played.</param>
public record ResistanceMissionResultDto(int MissionNumber, bool Succeeded, int? FailCount, IReadOnlyList<Guid> TeamPlayerIds);
