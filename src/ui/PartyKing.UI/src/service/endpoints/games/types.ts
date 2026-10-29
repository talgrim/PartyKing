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

// Mirrors PartyKing.Contract.V1.Games.RingoGameDto
export type RingoGameDto = {
  /** Seed of the card layout; the same seed gives the same layout on every device. */
  seed: number;
  /** Ids of the flipped cards (0-51) in flip order. */
  flippedCards: number[];
};
