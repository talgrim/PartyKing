import { useCallback, useSyncExternalStore } from 'react';

const CHANGE_EVENT = 'partyking:persistedFlagChanged';

// What could not be written to storage, so the flag still lasts until the page is closed
const unsaved = new Map<string, boolean>();

const subscribe = (onChange: () => void) => {
  window.addEventListener(CHANGE_EVENT, onChange);
  // Fires in the other tabs of the browser when one of them writes
  window.addEventListener('storage', onChange);
  return () => {
    window.removeEventListener(CHANGE_EVENT, onChange);
    window.removeEventListener('storage', onChange);
  };
};

/**
 * A boolean kept in localStorage, so the choice survives a reload. Falls back to the default when storage fails.
 * Every component (and every tab of the browser) that uses the same key sees a change at once.
 */
export const usePersistedFlag = (key: string, defaultValue: boolean) => {
  const read = useCallback((): boolean => {
    try {
      const stored = localStorage.getItem(key);
      if (stored !== null) return stored === 'true';
    } catch {
      // Storage is unavailable: fall through to what was set while it failed
    }
    return unsaved.get(key) ?? defaultValue;
  }, [key, defaultValue]);

  const value = useSyncExternalStore(subscribe, read, () => defaultValue);

  const update = useCallback(
    (next: boolean) => {
      try {
        localStorage.setItem(key, String(next));
        unsaved.delete(key);
      } catch {
        unsaved.set(key, next);
      }
      window.dispatchEvent(new Event(CHANGE_EVENT));
    },
    [key],
  );

  return [value, update] as const;
};
