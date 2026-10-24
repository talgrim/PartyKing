import { useCallback, useState } from 'react';

const clamp = (value: number, min: number, max: number) => Math.min(max, Math.max(min, value));

/**
 * A number kept in localStorage, so the choice survives a reload. Values are clamped to `min`..`max`, and a missing,
 * invalid or unreadable value falls back to the default.
 */
export const usePersistedNumber = (key: string, defaultValue: number, min: number, max: number) => {
  const [value, setValue] = useState<number>(() => {
    try {
      const stored = localStorage.getItem(key);
      const parsed = stored === null ? NaN : Number(stored);
      return Number.isFinite(parsed) ? clamp(parsed, min, max) : defaultValue;
    } catch {
      return defaultValue;
    }
  });

  const update = useCallback(
    (next: number) => {
      const clamped = clamp(next, min, max);
      setValue(clamped);
      try {
        localStorage.setItem(key, String(clamped));
      } catch {
        // Storage is unavailable: the value then only lasts until the page is closed
      }
    },
    [key, min, max],
  );

  return [value, update] as const;
};
