import { ReactNode } from 'react';

type Props = {
  showBoard: boolean;
  onChange: (showBoard: boolean) => void;
};

const buttonClass = (active: boolean) =>
  `px-3 py-2 cursor-pointer ${active ? 'bg-amber-400 text-slate-900 font-semibold' : 'bg-slate-900 text-slate-200'}`;

/** Switches a player between their own view and the board with the other players. */
export const ResistanceViewSwitch = ({ showBoard, onChange }: Props): ReactNode => (
  <div role="group" aria-label="View" className="flex rounded-md overflow-hidden">
    <button type="button" aria-pressed={!showBoard} onClick={() => onChange(false)} className={buttonClass(!showBoard)}>
      Player
    </button>
    <button type="button" aria-pressed={showBoard} onClick={() => onChange(true)} className={buttonClass(showBoard)}>
      Board
    </button>
  </div>
);
