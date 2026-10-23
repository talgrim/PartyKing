import { useCallback, useState } from 'react';

/** A boolean kept in localStorage, so the choice survives a reload. Falls back to the default when storage fails. */
export const usePersistedFlag = (key: string, defaultValue: boolean) => {
  const [value, setValue] = useState<boolean>(() => {
    try {
      const stored = localStorage.getItem(key);
      return stored === null ? defaultValue : stored === 'true';
    } catch {
      return defaultValue;
    }
  });

  const update = useCallback(
    (next: boolean) => {
      setValue(next);
      try {
        localStorage.setItem(key, String(next));
      } catch {
        // Storage is unavailable: the flag then only lasts until the page is closed
      }
    },
    [key],
  );

  return [value, update] as const;
};
