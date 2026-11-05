import { ReactNode, useState } from 'react';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { useTranslation } from 'react-i18next';

type Props = {
  game: ResistanceGameDto;
  onExamine: (targetPlayerId: string) => Promise<void>;
  onConfirm: () => Promise<void>;
};

/**
 * Inquisitor module: after missions 2 to 4 the holder of the token picks a player who never held it. The loyalty is
 * private to the holder (the server sends it only to them) and they confirm having seen it before the game goes on. The
 * others see nothing here; the phase line says who is examining.
 */
export const ResistanceInquisitor = ({ game, onExamine, onConfirm }: Props): ReactNode => {
  const { t } = useTranslation();
  const [target, setTarget] = useState('');
  const [submitting, setSubmitting] = useState(false);

  if (game.phase !== 'Inquisition' || game.yourPlayerId == null) return null;

  const picks = game.inquisitorPlayerId === game.yourPlayerId && game.inquisitionExaminerPlayerId == null;
  const result = game.yourInquisition;
  if (!picks && result == null) return null;

  const nameOf = (id: string) => game.players.find((player) => player.id === id)?.name ?? t('resistance.playerFallback');
  const options = game.players.filter((player) => !game.pastInquisitorPlayerIds.includes(player.id));

  const run = async (action: () => Promise<void>) => {
    setSubmitting(true);
    try {
      await action();
      setTarget('');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <section aria-label={t('resistance.inquisitor.title')} className="w-full flex flex-col gap-2 rounded-xl bg-slate-900 p-4 ring-1 ring-slate-700 shadow-lg">
      <h2 className="text-lg font-semibold text-slate-100">{t('resistance.inquisitor.title')}</h2>
      {picks && (
        <>
          <p className="text-sm text-slate-400">{t('resistance.inquisitor.effect')}</p>
          <label htmlFor="inquisitor-target" className="sr-only">
            {t('resistance.inquisitor.target')}
          </label>
          <select
            id="inquisitor-target"
            value={target}
            disabled={submitting}
            onChange={(event) => setTarget(event.target.value)}
            className="w-full rounded-lg bg-slate-800 px-3 py-2 text-slate-100 ring-1 ring-slate-700"
          >
            <option value="" disabled>
              {t('resistance.inquisitor.pick')}
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
            onClick={() => void run(() => onExamine(target))}
            className="w-full rounded-lg bg-emerald-500 px-4 py-2 font-semibold text-slate-900 transition-colors hover:bg-emerald-400 disabled:cursor-not-allowed disabled:bg-slate-700 disabled:text-slate-400"
          >
            {t('resistance.inquisitor.examine')}
          </button>
        </>
      )}
      {result != null && (
        <div className="flex flex-col gap-2 rounded-lg bg-slate-800 p-3">
          <p className="text-slate-100">
            {t('resistance.inquisitor.result', { name: nameOf(result.playerId) })}{' '}
            <strong className={result.role === 'Spy' ? 'text-red-400' : 'text-sky-300'}>
              {result.role === 'Spy' ? t('resistance.roles.spy') : t('resistance.roles.resistance')}
            </strong>
            .
          </p>
          <p className="text-sm text-slate-400">{t('resistance.inquisitor.private')}</p>
          <button
            type="button"
            disabled={submitting}
            onClick={() => void run(onConfirm)}
            className="rounded-lg bg-emerald-500 px-4 py-2 font-semibold text-slate-900 transition-colors hover:bg-emerald-400 disabled:cursor-not-allowed disabled:bg-slate-700 disabled:text-slate-400"
          >
            {t('resistance.inquisitor.seen')}
          </button>
        </div>
      )}
    </section>
  );
};
