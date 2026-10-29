import { ReactNode } from 'react';
import { RingoFlipDto } from '@/service/endpoints/games/types';
import { cardName } from './ringoCards';

type Props = {
  flips: RingoFlipDto[];
  open: boolean;
  onToggle: () => void;
};

const formatTime = (iso: string) =>
  new Date(iso).toLocaleTimeString('pl-PL', { hour: '2-digit', minute: '2-digit' });

/** The flipped cards so far, newest first, in a drawer on the right that is collapsed by default. */
export const RingoHistory = ({ flips, open, onToggle }: Props): ReactNode => (
  <>
    <button
      type="button"
      onClick={onToggle}
      aria-expanded={open}
      className={`absolute top-2 z-40 px-3 py-2 text-slate-200 bg-slate-900 rounded-md cursor-pointer hover:bg-slate-700 ${
        open ? 'right-[19rem]' : 'right-2'
      }`}
    >
      {open ? 'Hide history' : 'History'}
    </button>

    {open && (
      <aside
        aria-label="History"
        className="absolute inset-y-0 right-0 z-40 w-72 p-3 overflow-y-auto bg-slate-900 border-l border-slate-600 text-slate-200"
      >
        <h2 className="mb-2 text-lg font-semibold">History</h2>
        {flips.length === 0 ? (
          <p className="text-slate-400">No cards flipped yet.</p>
        ) : (
          <ul className="flex flex-col gap-1">
            {[...flips].reverse().map((flip) => (
              <li key={flip.cardId}>
                {formatTime(flip.flippedAt)}: {flip.flippedBy} odwrócił/a kartę: {cardName(flip.cardId)}
              </li>
            ))}
          </ul>
        )}
      </aside>
    )}
  </>
);
