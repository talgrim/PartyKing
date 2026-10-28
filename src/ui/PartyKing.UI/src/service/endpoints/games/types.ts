// Mirrors PartyKing.Contract.V1.Games.GameDto
export type GameDto = {
  /** Stable slug identifying the game. */
  id: string;
  name: string;
  description: string;
};

// Mirrors PartyKing.Contract.V1.Games.GameSuggestionsDto
export type GameSuggestionsDto = {
  /** Statements for the game, in Polish. */
  suggestions: string[];
  /** True when the language model was unavailable and the statements come from the built-in list. */
  fromFallback: boolean;
};
