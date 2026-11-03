namespace PartyKing.Contract.V1.Games;

/// <param name="Suggestions">Statements for the game, in the language of the request.</param>
/// <param name="FromFallback">True when the language model was unavailable and the statements come from the built-in list.</param>
public record GameSuggestionsDto(IReadOnlyList<string> Suggestions, bool FromFallback);
