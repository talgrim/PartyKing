namespace PartyKing.Contract.V1.Games.Resistance;

public record ResistanceCountRulesDto(
    int PlayerCount, int Spies, IReadOnlyList<int> TeamSizes, IReadOnlyList<int> RequiredFails);
