import { ReactNode, useState } from 'react';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { useTranslation } from 'react-i18next';

type Props = {
  game: ResistanceGameDto;
  onAssassinate: (targetPlayerId: string) => Promise<void>;
};

/**
 * Assassin module: after the third successful mission only the Assassin acts here and names the player they take for
 * the Commander, which ends the game. The other spies talk it over outside the app; everyone else waits (the phase line
 * says so).
 */
export const ResistanceAssassin = ({ game, onAssassinate }: Props): ReactNode => {
  const { t } = useTranslation();
  const [target, setTarget] = useState('');
  const [submitting, setSubmitting] = useState(false);

  if (game.phase !== 'Assassination' || game.yourPlayerId == null || game.yourCharacter !== 'Assassin') return null;

  const options = game.players.filter((player) => player.id !== game.yourPlayerId);

  const run = async () => {
    setSubmitting(true);
    try {
      await onAssassinate(target);
      setTarget('');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <section aria-label={t('resistance.assassin.title')} className="w-full flex flex-col gap-2 rounded-xl bg-slate-900 p-4 ring-1 ring-slate-700 shadow-lg">
      <h2 className="text-lg font-semibold text-slate-100">{t('resistance.assassin.title')}</h2>
      <p className="text-sm text-slate-400">{t('resistance.assassin.effect')}</p>
      <label htmlFor="assassin-target" className="sr-only">
        {t('resistance.assassin.target')}
      </label>
      <select
        id="assassin-target"
        value={target}
        disabled={submitting}
        onChange={(event) => setTarget(event.target.value)}
        className="w-full rounded-lg bg-slate-800 px-3 py-2 text-slate-100 ring-1 ring-slate-700"
      >
        <option value="" disabled>
          {t('resistance.assassin.pick')}
        </option>
        {options.map((player) => (
          <option key={player.id} value={player.id}>
            {player.name}
          </option>
        ))}
      </select>
      <button
        type="button"
        disabled={target === '' || submitting}
        onClick={() => void run()}
        className="w-full rounded-lg bg-rose-500 px-4 py-2 font-semibold text-slate-900 transition-colors hover:bg-rose-400 disabled:cursor-not-allowed disabled:bg-slate-700 disabled:text-slate-400"
      >
        {t('resistance.assassin.confirm')}
      </button>
    </section>
  );
};
