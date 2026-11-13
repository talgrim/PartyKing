/** The last minutes of a demo are shown as a warning. */
export const SOON_MS = 5 * 60 * 1000;
export const LAST_MS = 60 * 1000;

export type CountdownLevel = 'normal' | 'soon' | 'last';

/**
 * Milliseconds until the session ends, by the server's clock: `serverOffsetMs` is how far that clock is ahead of the
 * device's (negative when it is behind), taken once when the server's time arrived.
 */
export const countdownRemainingMs = (expiresAtMs: number, serverOffsetMs: number, nowMs: number): number =>
  expiresAtMs - (nowMs + serverOffsetMs);

export const countdownLevel = (remainingMs: number): CountdownLevel => {
  if (remainingMs <= LAST_MS) return 'last';
  return remainingMs <= SOON_MS ? 'soon' : 'normal';
};

/** "2:59:07" from an hour on, "4:05" below it. A started second counts as a whole one, so the end shows "0:00" last. */
export const formatCountdown = (remainingMs: number): string => {
  const totalSeconds = Math.max(0, Math.ceil(remainingMs / 1000));
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  const pad = (value: number) => String(value).padStart(2, '0');

  return hours > 0 ? `${hours}:${pad(minutes)}:${pad(seconds)}` : `${minutes}:${pad(seconds)}`;
};
