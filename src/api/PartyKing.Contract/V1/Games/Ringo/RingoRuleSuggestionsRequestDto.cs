namespace PartyKing.Contract.V1.Games.Ringo;

/// <param name="PreviousRules">Rules already suggested, so the new ones don't repeat them.</param>
public record RingoRuleSuggestionsRequestDto(IReadOnlyList<string>? PreviousRules);
