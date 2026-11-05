import { useCallback, useState } from 'react';

export type ResistanceView = 'player' | 'board' | 'queue';

const VIEW_KEY = 'partyking.resistanceView';
// Before the queue existed the choice was a boolean: true was the board
const LEGACY_BOARD_KEY = 'partyking.resistanceBoardView';

const isView = (value: string | null): value is ResistanceView =>
  value === 'player' || value === 'board' || value === 'queue';

const read = (): ResistanceView => {
  try {
    const stored = localStorage.getItem(VIEW_KEY);
    if (isView(stored)) return stored;
    return localStorage.getItem(LEGACY_BOARD_KEY) === 'true' ? 'board' : 'player';
  } catch {
    return 'player';
  }
};

/** Which view a player has on the phone: their own, the board or the music queue; kept in localStorage. */
export const useResistanceView = () => {
  const [view, setView] = useState<ResistanceView>(read);

  const update = useCallback((next: ResistanceView) => {
    setView(next);
    try {
      localStorage.setItem(VIEW_KEY, next);
    } catch {
      // Storage is unavailable: the choice then only lasts until the page is closed
    }
  }, []);

  return [view, update] as const;
};
