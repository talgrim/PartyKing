import { draftFromDrink, draftFromPreset, drinkAgain, drinkFromDraft, isDraftValid, newestFirst } from './bacDrinkDraft';
import { StoredDrink } from './bacStorage';

const NOW = Date.UTC(2025, 0, 1, 22, 0);
const MINUTE = 60_000;

const beer: StoredDrink = { id: 'a', kind: 'beer', volumeMl: 500, abvPercent: 5, startedAt: NOW - 40 * MINUTE };

describe('bacDrinkDraft', () => {
  it('starts a draft from the usual serving of a kind, started now', () => {
    expect(draftFromPreset('shot')).toEqual({ kind: 'shot', volumeMl: 40, abvPercent: 40, minutesAgo: 0, exactAt: null });
  });

  it('makes a drink of a draft, started the typed number of minutes ago', () => {
    const drink = drinkFromDraft({ ...draftFromPreset('beer'), minutesAgo: 20 }, false, NOW);

    expect(drink).toMatchObject({ kind: 'beer', volumeMl: 500, abvPercent: 5, startedAt: NOW - 20 * MINUTE });
  });

  it('uses the clock when the time is picked on it, and now when none was picked', () => {
    const picked = NOW - 90 * MINUTE;

    expect(drinkFromDraft({ ...draftFromPreset('wine'), exactAt: picked }, true, NOW)?.startedAt).toBe(picked);
    expect(drinkFromDraft(draftFromPreset('wine'), true, NOW)?.startedAt).toBe(NOW);
  });

  it('refuses a draft without a volume or with a strength out of range', () => {
    expect(drinkFromDraft({ ...draftFromPreset('beer'), volumeMl: null }, false, NOW)).toBeNull();
    expect(drinkFromDraft({ ...draftFromPreset('beer'), abvPercent: 120 }, false, NOW)).toBeNull();
    expect(isDraftValid({ ...draftFromPreset('beer'), volumeMl: 0 })).toBe(false);
    expect(isDraftValid(draftFromPreset('beer'))).toBe(true);
  });

  it('keeps the start of an entry that is edited and saved without touching its time', () => {
    const edited = drinkFromDraft({ ...draftFromDrink(beer), volumeMl: 330 }, false, NOW + 7 * MINUTE, beer.id);

    expect(edited).toEqual({ ...beer, volumeMl: 330 });
  });

  it('moves the start of an entry whose minutes ago were changed', () => {
    const edited = drinkFromDraft({ ...draftFromDrink(beer), minutesAgo: 10 }, false, NOW, beer.id);

    expect(edited?.startedAt).toBe(NOW - 10 * MINUTE);
  });

  it('drinks the same again: the same values, a new id, started now', () => {
    const again = drinkAgain(beer, NOW);

    expect(again).toMatchObject({ kind: 'beer', volumeMl: 500, abvPercent: 5, startedAt: NOW });
    expect(again.id).not.toBe(beer.id);
  });

  it('puts the drink started last first, whatever order they were added in', () => {
    const early = { ...beer, id: 'early', startedAt: NOW - 90 * MINUTE };
    const late = { ...beer, id: 'late', startedAt: NOW - 5 * MINUTE };
    const middle = { ...beer, id: 'middle', startedAt: NOW - 40 * MINUTE };

    expect(newestFirst([early, late, middle]).map((drink) => drink.id)).toEqual(['late', 'middle', 'early']);
  });

  it('puts the later added of two drinks started together on top, and leaves its input alone', () => {
    const first = { ...beer, id: 'first' };
    const second = { ...beer, id: 'second' };
    const drinks = [first, second];

    expect(newestFirst(drinks).map((drink) => drink.id)).toEqual(['second', 'first']);
    expect(drinks).toEqual([first, second]);
  });
});
