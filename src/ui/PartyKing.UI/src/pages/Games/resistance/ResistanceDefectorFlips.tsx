import { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

type Props = {
  /** The switch cards flipped so far, true for Change Allegiance; the first one belongs to mission 3. */
  flips: readonly boolean[];
};

/** The first mission whose start flips a switch card (Defector module). */
const FIRST_FLIP_MISSION = 3;

/** The switch cards flipped so far, public: everybody sees whether a switch happened, but not who the Defectors are. */
export const ResistanceDefectorFlips = ({ flips }: Props): ReactNode => {
  const { t } = useTranslation();
  if (flips.length === 0) {
    return null;
  }

  return (
    <div className="flex flex-wrap items-center justify-center gap-2 text-sm text-slate-200">
      <span>{t('resistance.defectorFlips.title')}</span>
      <ul aria-label={t('resistance.defectorFlips.list')} className="flex flex-wrap gap-2">
        {flips.map((change, index) => (
          <li
            key={index}
            className={`rounded px-2 py-0.5 ${change ? 'bg-rose-700 text-white' : 'bg-slate-700 text-slate-100'}`}
          >
            {t(change ? 'resistance.defectorFlips.change' : 'resistance.defectorFlips.noChange', {
              mission: index + FIRST_FLIP_MISSION,
            })}
          </li>
        ))}
      </ul>
    </div>
  );
};
