namespace PartyKing.Contract.V1.Games.Ringo;

/// <param name="PreviousRules">Rules already suggested, so the new ones don't repeat them.</param>
public record RingoRuleSuggestionsRequest(IReadOnlyList<string>? PreviousRules);
