import { CARD_COUNT, createLayout, getCard } from './ringoLayout';

describe('getCard', () => {
  it('covers every rank of every suit exactly once', () => {
    const faces = Array.from({ length: CARD_COUNT }, (_, id) => getCard(id)).map((card) => `${card.rank}${card.suit}`);

    expect(new Set(faces).size).toBe(52);
    expect(getCard(0)).toMatchObject({ rank: '2', suit: '♠', isRed: false });
    expect(getCard(12)).toMatchObject({ rank: 'A', suit: '♠' });
    expect(getCard(13)).toMatchObject({ rank: '2', suit: '♥', isRed: true });
    expect(getCard(51)).toMatchObject({ rank: 'A', suit: '♣', isRed: false });
  });
});

describe('createLayout', () => {
  it('places all 52 cards with distinct stacking orders', () => {
    const layout = createLayout(42);

    expect(layout.map((placement) => placement.id)).toEqual(Array.from({ length: 52 }, (_, i) => i));
    expect(layout.map((placement) => placement.z).sort((a, b) => a - b)).toEqual(
      Array.from({ length: 52 }, (_, i) => i + 1),
    );
  });

  it('keeps every card inside the board', () => {
    for (const { x, y } of createLayout(7)) {
      expect(x).toBeGreaterThanOrEqual(0);
      expect(x).toBeLessThan(1);
      expect(y).toBeGreaterThanOrEqual(0);
      expect(y).toBeLessThan(1);
    }
  });

  it('gives the same layout for the same seed and another one for another seed', () => {
    expect(createLayout(123)).toEqual(createLayout(123));
    expect(createLayout(123)).not.toEqual(createLayout(124));
  });
});
