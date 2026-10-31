import { ReactNode, useState } from 'react';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { PLOT_CARDS } from './resistancePlotCards';
import { useTranslation } from 'react-i18next';

type Props = {
  game: ResistanceGameDto;
  onPeek: (cardId: number, targetPlayerId: string) => Promise<void>;
};

/**
 * Keeping a Close Eye: in the peek window after the mission is played its holder looks at one team member's mission card.
 * Not your own, and a card nobody has looked at yet. The play is public; the card seen stays with the peeker.
 */
export const ResistanceCloseEye = ({ game, onPeek }: Props): ReactNode => {
  const { t } = useTranslation();
  const [target, setTarget] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const card = game.yourPlotCards.find((x) => x.type === 'KeepingCloseEye' && !x.toDeal);
  const peeks = game.yourPeeks;
  const { name, effect, Icon } = PLOT_CARDS.KeepingCloseEye;
  const nameOf = (id: string) => game.players.find((player) => player.id === id)?.name ?? '?';

  const inWindow = game.phase === 'PeekWindow';
  if (!(inWindow || game.phase === 'MissionResult') || (card == null && peeks.length === 0)) return null;

  const looked = game.playedPlotCards
    .filter((x) => x.type === 'KeepingCloseEye' && x.missionNumber === game.missionNumber)
    .map((x) => x.targetPlayerId);
  const options = game.players.filter(
    (player) => player.id !== game.yourPlayerId && game.teamPlayerIds.includes(player.id) && !looked.includes(player.id),
  );

  const peek = async () => {
    if (card == null) return;
    setSubmitting(true);
    try {
      await onPeek(card.id, target);
      setTarget('');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <section aria-label={name} className="w-full flex items-center gap-3 rounded-xl bg-slate-900 p-4 ring-1 ring-slate-700 shadow-lg">
      <Icon className="w-16 shrink-0" title={name} />
      <div className="min-w-0 flex-1">
        <p className="font-medium text-slate-100">{name}</p>
        {peeks.map((seen) => (
          <p key={seen.playerId} className="text-sm text-slate-100">
            {t('resistance.closeEye.seen', { name: nameOf(seen.playerId), card: seen.success ? t('resistance.missionCard.success') : t('resistance.missionCard.fail') })}
          </p>
        ))}
        {inWindow && card != null && options.length > 0 && (
          <>
            <p className="mb-1 text-sm text-slate-400">{effect}</p>
            <label htmlFor="close-eye-target" className="sr-only">
              {t('resistance.closeEye.target')}
            </label>
            <select
              id="close-eye-target"
              value={target}
              disabled={submitting}
              onChange={(event) => setTarget(event.target.value)}
              className="w-full rounded-lg bg-slate-800 px-3 py-2 text-slate-100 ring-1 ring-slate-700"
            >
              <option value="" disabled>
                {t('resistance.closeEye.pick')}
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
              onClick={() => void peek()}
              className="mt-2 w-full rounded-lg bg-emerald-500 px-4 py-2 font-semibold text-slate-900 transition-colors hover:bg-emerald-400 disabled:cursor-not-allowed disabled:bg-slate-700 disabled:text-slate-400"
            >
              {t('resistance.closeEye.peek')}
            </button>
          </>
        )}
      </div>
    </section>
  );
};
