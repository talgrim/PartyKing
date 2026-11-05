import { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { ResistanceView } from './useResistanceView';

type Props = {
  value: ResistanceView;
  onChange: (view: ResistanceView) => void;
  /** The views on offer, in order; from 1024 px wide the player view is always on screen and is left out. */
  views?: readonly ResistanceView[];
};

const ALL_VIEWS: readonly ResistanceView[] = ['player', 'board', 'queue'];

const buttonClass = (active: boolean) =>
  `px-3 py-2 cursor-pointer ${active ? 'bg-amber-400 text-slate-900 font-semibold' : 'bg-slate-900 text-slate-200'}`;

/** Switches a player between their own view, the board with the other players and the music queue. */
export const ResistanceViewSwitch = ({ value, onChange, views = ALL_VIEWS }: Props): ReactNode => {
  const { t } = useTranslation();

  return (
    <div role="group" aria-label={t('resistance.viewSwitch.label')} className="flex rounded-md overflow-hidden">
      {views.map((view) => (
        <button
          key={view}
          type="button"
          aria-pressed={value === view}
          onClick={() => onChange(view)}
          className={buttonClass(value === view)}
        >
          {t(`resistance.viewSwitch.${view}`)}
        </button>
      ))}
    </div>
  );
};
