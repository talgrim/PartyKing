/** A jump this far from where playback should be counts as a seek; smaller gaps are timer and report jitter. */
export const SEEK_TOLERANCE_MS = 2000;

/**
 * Follows where a player is from the positions it reports now and then, and tells when it jumped: a position that
 * is far from where the previous one would have counted on to is a seek.
 */
export const createPositionTracker = (now: () => number = () => performance.now()) => {
  let anchor: { positionMs: number; at: number; paused: boolean } | null = null;

  const expected = () =>
    anchor === null ? 0 : anchor.paused ? anchor.positionMs : anchor.positionMs + (now() - anchor.at);

  return {
    /** Records a reported position and returns whether it is a seek. The first one after a reset never is. */
    observe(positionMs: number, paused: boolean): boolean {
      const seeked = anchor !== null && Math.abs(positionMs - expected()) > SEEK_TOLERANCE_MS;
      anchor = { positionMs, at: now(), paused };
      return seeked;
    },

    /** Forgets the position, e.g. when another song starts. */
    reset() {
      anchor = null;
    },
  };
};
