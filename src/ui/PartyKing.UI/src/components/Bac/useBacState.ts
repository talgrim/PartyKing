import { useCallback, useEffect, useState } from 'react';
import { BacState, readBacState, writeBacState } from './bacStorage';

/**
 * What the calculator remembers (the profile and the drinks), kept in localStorage so it survives a reload. Nothing
 * leaves the browser: a visitor has no account to keep it in.
 */
export const useBacState = () => {
  const [state, setState] = useState<BacState>(() => readBacState(Date.now()));

  useEffect(() => writeBacState(state), [state]);

  const update = useCallback((patch: Partial<BacState>) => setState((current) => ({ ...current, ...patch })), []);

  return [state, update] as const;
};
