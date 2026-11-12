import { fromLocalInputValue, fromMinutesAgo, minutesAgo, toLocalInputValue } from './bacTime';

const NOW = Date.UTC(2025, 0, 1, 22, 0);

describe('bacTime', () => {
  it('counts the minutes since a moment, never below zero', () => {
    expect(minutesAgo(NOW - 90 * 60_000, NOW)).toBe(90);
    expect(minutesAgo(NOW + 60_000, NOW)).toBe(0);
  });

  it('goes back and forth between minutes ago and a moment', () => {
    expect(minutesAgo(fromMinutesAgo(45, NOW), NOW)).toBe(45);
  });

  it('round-trips a moment through a datetime-local value, to the minute', () => {
    const value = toLocalInputValue(NOW);

    expect(value).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/);
    expect(fromLocalInputValue(value)).toBe(NOW);
  });

  it('has no moment for an empty value', () => {
    expect(fromLocalInputValue('')).toBeNull();
  });
});
