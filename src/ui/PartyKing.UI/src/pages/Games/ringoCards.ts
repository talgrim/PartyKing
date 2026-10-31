import { i18n } from '@/i18n';
import { RingoFlipDto } from '@/service/endpoints/games/types';
import { getCard } from './ringoLayout';

/** The fourth king ends the game. */
export const KINGS_TO_END = 4;

// The names are looked up when asked for, so they follow the UI language
const SUIT_KEYS = {
  '♠': 'ringo.suit.spades',
  '♥': 'ringo.suit.hearts',
  '♦': 'ringo.suit.diamonds',
  '♣': 'ringo.suit.clubs',
} as const;

const RANK_KEYS = {
  '2': 'ringo.rank.2',
  '3': 'ringo.rank.3',
  '4': 'ringo.rank.4',
  '5': 'ringo.rank.5',
  '6': 'ringo.rank.6',
  '7': 'ringo.rank.7',
  '8': 'ringo.rank.8',
  '9': 'ringo.rank.9',
  '10': 'ringo.rank.10',
  J: 'ringo.rank.J',
  Q: 'ringo.rank.Q',
  K: 'ringo.rank.K',
  A: 'ringo.rank.A',
} as const;

const EFFECT_KEYS = {
  '2': 'ringo.effect.2',
  '3': 'ringo.effect.3',
  '4': 'ringo.effect.4',
  '5': 'ringo.effect.5',
  '6': 'ringo.effect.6',
  '7': 'ringo.effect.7',
  '8': 'ringo.effect.8',
  '9': 'ringo.effect.9',
  '10': 'ringo.effect.10',
  J: 'ringo.effect.J',
  Q: 'ringo.effect.Q',
  A: 'ringo.effect.A',
} as const;

export const cardName = (cardId: number): string => {
  const { rank, suit } = getCard(cardId);
  return i18n.t('ringo.cardName', {
    rank: i18n.t(RANK_KEYS[rank as keyof typeof RANK_KEYS]),
    suit: i18n.t(SUIT_KEYS[suit as keyof typeof SUIT_KEYS]),
  });
};

/** Which king (1-4) of the game the card is, counted in flip order; 0 for a card that isn't a king. */
export const kingNumber = (flips: RingoFlipDto[], cardId: number): number => {
  const kings = flips.filter((flip) => getCard(flip.cardId).rank === 'K');
  return kings.findIndex((flip) => flip.cardId === cardId) + 1;
};

/** The flip of the fourth king, which ends the game; undefined while the game goes on. */
export const endingFlip = (flips: RingoFlipDto[]): RingoFlipDto | undefined =>
  flips.filter((flip) => getCard(flip.cardId).rank === 'K')[KINGS_TO_END - 1];

export const cardEffect = (cardId: number, flips: RingoFlipDto[]): string => {
  const { rank } = getCard(cardId);
  if (rank !== 'K') return i18n.t(EFFECT_KEYS[rank as keyof typeof EFFECT_KEYS]);

  const number = kingNumber(flips, cardId);
  return number >= KINGS_TO_END
    ? i18n.t('ringo.effect.lastKing')
    : i18n.t('ringo.effect.king', { number, total: KINGS_TO_END });
};

/** The cards that already left the table into the kings or aces pile (every flipped card but the pending one). */
export const pileCards = (flips: RingoFlipDto[], pendingCardId: number | null) => {
  const gone = flips.map((flip) => getCard(flip.cardId)).filter((card) => card.id !== pendingCardId);
  return {
    kings: gone.filter((card) => card.rank === 'K'),
    aces: gone.filter((card) => card.rank === 'A'),
  };
};
