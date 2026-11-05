import { useCallback, useSyncExternalStore } from 'react';

const CHANGE_EVENT = 'partyking:persistedNumberChanged';

const clamp = (value: number, min: number, max: number) => Math.min(max, Math.max(min, value));

// What could not be written to storage, so the value still lasts until the page is closed
const unsaved = new Map<string, number>();

const subscribe = (onChange: () => void) => {
  window.addEventListener(CHANGE_EVENT, onChange);
  // Fires in the other tabs of the browser when one of them writes, which is how a volume set on one page reaches
  // the player on another
  window.addEventListener('storage', onChange);
  return () => {
    window.removeEventListener(CHANGE_EVENT, onChange);
    window.removeEventListener('storage', onChange);
  };
};

/**
 * A number kept in localStorage, so the choice survives a reload. Values are clamped to `min`..`max`, and a missing,
 * invalid or unreadable value falls back to the default. Every component (and every tab of the browser) that uses
 * the same key sees a change at once.
 */
export const usePersistedNumber = (key: string, defaultValue: number, min: number, max: number) => {
  const read = useCallback((): number => {
    try {
      const stored = localStorage.getItem(key);
      const parsed = stored === null ? NaN : Number(stored);
      if (Number.isFinite(parsed)) {
        return clamp(parsed, min, max);
      }
    } catch {
      // Storage is unavailable: fall through to what was set while it failed
    }
    return unsaved.get(key) ?? defaultValue;
  }, [key, min, max, defaultValue]);

  const value = useSyncExternalStore(subscribe, read, () => defaultValue);

  const update = useCallback(
    (next: number) => {
      const clamped = clamp(next, min, max);
      try {
        localStorage.setItem(key, String(clamped));
        unsaved.delete(key);
      } catch {
        unsaved.set(key, clamped);
      }
      window.dispatchEvent(new Event(CHANGE_EVENT));
    },
    [key, min, max],
  );

  return [value, update] as const;
};
