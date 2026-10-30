import { cardEffect, endingFlip, cardName, kingNumber, pileCards } from './ringoCards';

const flip = (cardId: number) => ({ cardId, flippedAt: '2026-10-29T20:02:00Z', flippedBy: 'admin' });

describe('ringoCards', () => {
  it('names a card in Polish', () => {
    expect(cardName(12)).toBe('As Pik');
    expect(cardName(0)).toBe('Dwójka Pik');
    expect(cardName(13 + 8)).toBe('Dziesiątka Kier');
    expect(cardName(26 + 9)).toBe('Walet Karo');
    expect(cardName(39 + 11)).toBe('Król Trefl');
  });

  it('has an effect for every rank', () => {
    for (let id = 0; id < 13; id++) {
      expect(cardEffect(id, [flip(id)]).length).toBeGreaterThan(10);
    }
  });

  it('counts the kings in flip order, and the fourth ends the game', () => {
    const flips = [11, 5, 24, 37, 50].map(flip);

    expect(kingNumber(flips, 11)).toBe(1);
    expect(kingNumber(flips, 50)).toBe(4);
    expect(kingNumber(flips, 5)).toBe(0);
    expect(cardEffect(24, flips)).toContain('2. król');
    expect(cardEffect(50, flips)).toContain('Koniec gry');
  });

  it('piles up the kings and aces that are not pending', () => {
    const flips = [11, 12, 5, 24].map(flip);

    const { kings, aces } = pileCards(flips, 24);

    expect(kings.map((card) => card.id)).toEqual([11]);
    expect(aces.map((card) => card.id)).toEqual([12]);
  });

  it('ends the game with the fourth king, and knows who flipped it', () => {
    const flips = [11, 5, 24, 37].map(flip);
    expect(endingFlip(flips)).toBeUndefined();

    const ending = { ...flip(50), flippedBy: 'guest' };

    expect(endingFlip([...flips, ending])).toBe(ending);
  });
});
