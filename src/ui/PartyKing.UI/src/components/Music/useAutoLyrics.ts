import { useCallback, useSyncExternalStore } from 'react';

const STORAGE_KEY = 'partyking.autoLyrics';
const CHANGE_EVENT = 'partyking:autoLyricsChanged';

const read = (): boolean => {
  try {
    return localStorage.getItem(STORAGE_KEY) === 'true';
  } catch {
    return false;
  }
};

const subscribe = (onChange: () => void) => {
  window.addEventListener(CHANGE_EVENT, onChange);
  window.addEventListener('storage', onChange);
  return () => {
    window.removeEventListener(CHANGE_EVENT, onChange);
    window.removeEventListener('storage', onChange);
  };
};

/**
 * Whether the drawer follows the lyrics by itself, stored in localStorage (per browser, off by default). Unlike
 * `usePersistedFlag`, every component that uses it sees a change at once: the settings dialog sets it and the
 * session page reacts.
 */
export const useAutoLyrics = () => {
  const value = useSyncExternalStore(subscribe, read, () => false);

  const update = useCallback((next: boolean) => {
    try {
      localStorage.setItem(STORAGE_KEY, String(next));
    } catch {
      // Storage is unavailable: the change is then only seen until the page is closed
    }
    window.dispatchEvent(new Event(CHANGE_EVENT));
  }, []);

  return [value, update] as const;
};
