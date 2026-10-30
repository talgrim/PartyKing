export const CARD_COUNT = 52;

const RANKS = ['2', '3', '4', '5', '6', '7', '8', '9', '10', 'J', 'Q', 'K', 'A'];
const SUITS = ['♠', '♥', '♦', '♣'];
const RED_SUITS = new Set(['♥', '♦']);
const MAX_TILT_DEGREES = 35;

export type RingoCard = { id: number; rank: string; suit: string; isRed: boolean };

export type CardPlacement = {
  id: number;
  /** Position of the card inside the free space of the board, 0 (left/top) to 1 (right/bottom). */
  x: number;
  y: number;
  rotation: number;
  /** Stacking order, 1 (bottom) to 52 (top); every card has its own. */
  z: number;
};

export const getCard = (id: number): RingoCard => {
  const suit = SUITS[Math.floor(id / RANKS.length)];
  return { id, rank: RANKS[id % RANKS.length], suit, isRed: RED_SUITS.has(suit) };
};

// mulberry32: a tiny seeded generator, so a seed always gives the same layout
const createRandom = (seed: number) => {
  let state = seed >>> 0;
  return () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let t = state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
};

/** Scatters all 52 cards over the board. Cards overlap, so some end up completely covered. */
export const createLayout = (seed: number): CardPlacement[] => {
  const random = createRandom(seed);

  const stacking = Array.from({ length: CARD_COUNT }, (_, i) => i + 1);
  for (let i = stacking.length - 1; i > 0; i--) {
    const j = Math.floor(random() * (i + 1));
    [stacking[i], stacking[j]] = [stacking[j], stacking[i]];
  }

  return Array.from({ length: CARD_COUNT }, (_, id) => ({
    id,
    x: random(),
    y: random(),
    rotation: (random() * 2 - 1) * MAX_TILT_DEGREES,
    z: stacking[id],
  }));
};
