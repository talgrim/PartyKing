import { LyricsLineDto } from '@/service/endpoints/lyrics/types';

/** The host nudges the lyrics by this much per press, to line them up with the sound they hear. */
export const OFFSET_STEP_MS = 500;
export const MAX_OFFSET_MS = 10000;

/** The lyrics text size as a percentage of the default, changed by the magnifier buttons. */
export const FONT_SCALE_DEFAULT = 100;
export const FONT_SCALE_STEP = 20;
export const FONT_SCALE_MIN = 60;
export const FONT_SCALE_MAX = 400;

/** Index of the line being sung at `positionMs`: the last one that has started. -1 before the first line. */
export const activeLineIndex = (lines: LyricsLineDto[], positionMs: number): number => {
  let low = 0;
  let high = lines.length - 1;
  let found = -1;

  while (low <= high) {
    const middle = (low + high) >> 1;
    if (lines[middle].timeMs <= positionMs) {
      found = middle;
      low = middle + 1;
    } else {
      high = middle - 1;
    }
  }

  return found;
};

export const formatOffset = (offsetMs: number): string => {
  const sign = offsetMs > 0 ? '+' : offsetMs < 0 ? '−' : '';
  return `${sign}${(Math.abs(offsetMs) / 1000).toFixed(1)} s`;
};
