import { countdownLevel, countdownRemainingMs, formatCountdown, LAST_MS, SOON_MS } from './demoCountdown';

describe('countdownRemainingMs', () => {
  it('counts to the end by the server clock, not the one of the device', () => {
    const expiresAt = 1_000_000;
    const deviceNow = 400_000;

    expect(countdownRemainingMs(expiresAt, 0, deviceNow)).toBe(600_000);
    // The server is a minute ahead of the device, so there is a minute less left
    expect(countdownRemainingMs(expiresAt, 60_000, deviceNow)).toBe(540_000);
    expect(countdownRemainingMs(expiresAt, -60_000, deviceNow)).toBe(660_000);
  });
});

describe('countdownLevel', () => {
  it.each([
    [SOON_MS + 1, 'normal'],
    [SOON_MS, 'soon'],
    [LAST_MS + 1, 'soon'],
    [LAST_MS, 'last'],
    [0, 'last'],
    [-1000, 'last'],
  ])('is warned about at %i ms left: %s', (remainingMs, level) => {
    expect(countdownLevel(remainingMs)).toBe(level);
  });
});

describe('formatCountdown', () => {
  it.each([
    [3 * 3600 * 1000, '3:00:00'],
    [2 * 3600 * 1000 + 59 * 60 * 1000 + 7000, '2:59:07'],
    [3600 * 1000, '1:00:00'],
    [3600 * 1000 - 1000, '59:59'],
    [4 * 60 * 1000 + 5000, '4:05'],
    [500, '0:01'],
    [0, '0:00'],
    [-5000, '0:00'],
  ])('shows %i ms as %s', (remainingMs, text) => {
    expect(formatCountdown(remainingMs)).toBe(text);
  });
});
