import { DEFAULT_BAC_STATE, BacState, parseBacState, serializeBacState, newDrink } from './bacStorage';

const NOW = Date.UTC(2025, 0, 1, 22, 0);
const HOUR = 3_600_000;

describe('parseBacState', () => {
  it('is the default when nothing is saved', () => {
    expect(parseBacState(null, NOW)).toEqual(DEFAULT_BAC_STATE);
  });

  it.each(['not json', '42', 'null', '{}', JSON.stringify({ version: 99 })])('is the default for %s', (raw) => {
    expect(parseBacState(raw, NOW)).toEqual(DEFAULT_BAC_STATE);
  });

  it('restores what it saved', () => {
    const state: BacState = {
      weightKg: 75,
      sex: 'female',
      heightCm: 170,
      age: 28,
      exactTime: true,
      drinks: [{ ...newDrink('wine', NOW - HOUR), id: 'a' }],
    };

    expect(parseBacState(serializeBacState(state), NOW)).toEqual(state);
  });

  it('drops a field out of its range and keeps the rest', () => {
    const raw = JSON.stringify({ version: 1, weightKg: 5, sex: 'female', heightCm: 175, age: 'old', drinks: [] });

    expect(parseBacState(raw, NOW)).toMatchObject({ weightKg: null, sex: 'female', heightCm: 175, age: null });
  });

  it('forgets the drinks older than a day and the broken ones', () => {
    const fresh = { ...newDrink('beer', NOW - HOUR), id: 'fresh' };
    const old = { ...newDrink('beer', NOW - 25 * HOUR), id: 'old' };
    const broken = { id: 'broken', kind: 'beer', volumeMl: -3, abvPercent: 5, startedAt: NOW };
    const raw = JSON.stringify({ version: 1, drinks: [fresh, old, broken, 'x', null] });

    expect(parseBacState(raw, NOW).drinks).toEqual([fresh]);
  });
});
