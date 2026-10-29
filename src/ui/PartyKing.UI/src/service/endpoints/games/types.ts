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
  /** The flipped cards in flip order. */
  flips: RingoFlipDto[];
  /** The last flipped card (0-51) until the host or an admin continues past it; null when none waits for that. */
  pendingCardId: number | null;
};

// Mirrors PartyKing.Contract.V1.Games.RingoFlipDto
export type RingoFlipDto = {
  /** Card id (0-51). */
  cardId: number;
  /** ISO timestamp. */
  flippedAt: string;
  flippedBy: string;
};
