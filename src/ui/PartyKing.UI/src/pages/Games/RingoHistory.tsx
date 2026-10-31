import { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { RingoFlipDto } from '@/service/endpoints/games/types';
import { cardName } from './ringoCards';

type Props = {
  flips: RingoFlipDto[];
  open: boolean;
  onToggle: () => void;
};

const formatTime = (iso: string, language: string) =>
  new Date(iso).toLocaleTimeString(language, { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' });

/** The flipped cards so far, newest first, in a drawer on the right that is collapsed by default. */
export const RingoHistory = ({ flips, open, onToggle }: Props): ReactNode => {
  const { t, i18n } = useTranslation();

  return (
    <>
      <button
        type="button"
        onClick={onToggle}
        aria-expanded={open}
        className={`absolute top-2 z-40 px-3 py-2 text-slate-200 bg-slate-900 rounded-md cursor-pointer hover:bg-slate-700 ${
          open ? 'right-[19rem]' : 'right-2'
        }`}
      >
        {open ? t('ringo.historyHide') : t('ringo.historyShow')}
      </button>

      {open && (
        <aside
          aria-label={t('ringo.historyShow')}
          className="absolute inset-y-0 right-0 z-40 w-72 p-3 overflow-y-auto bg-slate-900 border-l border-slate-600 text-slate-200"
        >
          <h2 className="mb-2 text-lg font-semibold">{t('ringo.historyShow')}</h2>
          {flips.length === 0 ? (
            <p className="text-slate-400">{t('ringo.historyEmpty')}</p>
          ) : (
            <ul className="flex flex-col gap-1">
              {[...flips].reverse().map((flip) => (
                <li key={flip.cardId}>
                  {t('ringo.historyEntry', {
                    time: formatTime(flip.flippedAt, i18n.language),
                    name: flip.flippedBy,
                    card: cardName(flip.cardId),
                  })}
                </li>
              ))}
            </ul>
          )}
        </aside>
      )}
    </>
  );
};
