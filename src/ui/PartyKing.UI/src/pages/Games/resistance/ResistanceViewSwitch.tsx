import { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

type Props = {
  showBoard: boolean;
  onChange: (showBoard: boolean) => void;
};

const buttonClass = (active: boolean) =>
  `px-3 py-2 cursor-pointer ${active ? 'bg-amber-400 text-slate-900 font-semibold' : 'bg-slate-900 text-slate-200'}`;

/** Switches a player between their own view and the board with the other players. */
export const ResistanceViewSwitch = ({ showBoard, onChange }: Props): ReactNode => {
  const { t } = useTranslation();

  return (
  <div role="group" aria-label={t('resistance.viewSwitch.label')} className="flex rounded-md overflow-hidden">
    <button type="button" aria-pressed={!showBoard} onClick={() => onChange(false)} className={buttonClass(!showBoard)}>
      {t('resistance.viewSwitch.player')}
    </button>
    <button type="button" aria-pressed={showBoard} onClick={() => onChange(true)} className={buttonClass(showBoard)}>
      {t('resistance.viewSwitch.board')}
    </button>
  </div>
  );
};
