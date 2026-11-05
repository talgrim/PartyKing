import { describe, expect, it } from 'vitest';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { assassinCharactersProblem } from './assassinCharacters';

const SPIES: Record<number, number> = { 5: 2, 6: 2, 7: 3, 8: 3, 9: 3, 10: 4 };

const lobby = (players: number, rules: Record<string, boolean> = {}): ResistanceGameDto =>
  ({
    players: Array.from({ length: players }, (_, i) => ({ id: `p${i}`, name: `P${i}` })),
    rules: {
      playerCounts: Object.entries(SPIES).map(([count, spies]) => ({ playerCount: Number(count), spies })),
      assassinEnabled: true,
      bodyGuardEnabled: false,
      deepCoverEnabled: false,
      blindSpyEnabled: false,
      falseCommanderEnabled: false,
      ...rules,
    },
  }) as unknown as ResistanceGameDto;

describe('assassinCharactersProblem', () => {
  it('finds nothing wrong without the module, with the basic pair, or with too few players', () => {
    expect(assassinCharactersProblem(lobby(5, { assassinEnabled: false, deepCoverEnabled: true, blindSpyEnabled: true }))).toBeNull();
    expect(assassinCharactersProblem(lobby(5))).toBeNull();
    expect(assassinCharactersProblem(lobby(3, { deepCoverEnabled: true, blindSpyEnabled: true }))).toBeNull();
  });

  it.each([
    [5, { deepCoverEnabled: true, blindSpyEnabled: true }],
    [6, { deepCoverEnabled: true, falseCommanderEnabled: true }],
    [7, { deepCoverEnabled: true, blindSpyEnabled: true, falseCommanderEnabled: true }],
  ])('flags too many spy characters for %i players', (players, rules) => {
    expect(assassinCharactersProblem(lobby(players, rules))).toBe('tooManySpyCharacters');
  });

  it.each([
    [5, { deepCoverEnabled: true }],
    [7, { deepCoverEnabled: true, blindSpyEnabled: true }],
    [10, { deepCoverEnabled: true, blindSpyEnabled: true, falseCommanderEnabled: true }],
  ])('accepts spy characters that fit %i players', (players, rules) => {
    expect(assassinCharactersProblem(lobby(players, rules))).toBeNull();
  });

  it('wants cover for a Body Guard at 5 players only', () => {
    expect(assassinCharactersProblem(lobby(5, { bodyGuardEnabled: true }))).toBe('bodyGuardNeedsCover');
    expect(assassinCharactersProblem(lobby(5, { bodyGuardEnabled: true, blindSpyEnabled: true }))).toBe('bodyGuardNeedsCover');
    expect(assassinCharactersProblem(lobby(5, { bodyGuardEnabled: true, deepCoverEnabled: true }))).toBeNull();
    expect(assassinCharactersProblem(lobby(5, { bodyGuardEnabled: true, falseCommanderEnabled: true }))).toBeNull();
    expect(assassinCharactersProblem(lobby(6, { bodyGuardEnabled: true }))).toBeNull();
  });
});
