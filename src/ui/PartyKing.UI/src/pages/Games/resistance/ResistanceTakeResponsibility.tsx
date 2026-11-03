import { ReactNode, useState } from 'react';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { PLOT_CARDS } from './resistancePlotCards';
import { useTranslation } from 'react-i18next';

type Props = {
  game: ResistanceGameDto;
  onTake: (cardId: number, targetPlayerId: string, takeOpinionMaker: boolean | null) => Promise<void>;
};

/** The steps of the round in which Take Responsibility can be played: not while a team is voted on. */
const PLAYABLE_PHASES = ['SelectingTeam', 'PlayingMission', 'PeekWindow', 'MissionResult'];

/**
 * Take Responsibility: its holder takes a card from another player. A face-up Opinion Maker can be taken on purpose;
 * a hand card is picked at random by the server, so nobody sees what the others hold. With both on offer the holder
 * chooses which; otherwise the server takes what there is (or nothing).
 */
export const ResistanceTakeResponsibility = ({ game, onTake }: Props): ReactNode => {
  const { t } = useTranslation();
  const [target, setTarget] = useState('');
  const [choice, setChoice] = useState<boolean | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const card = game.yourPlotCards.find((x) => x.type === 'TakeResponsibility' && !x.toDeal);

  if (card == null || game.phase == null || !PLAYABLE_PHASES.includes(game.phase)) return null;

  const { name, effect, Icon } = PLOT_CARDS.TakeResponsibility;
  const others = game.players.filter((player) => player.id !== game.yourPlayerId);
  const hasOpinionMaker = game.opinionMakerPlayerIds.includes(target);
  const handSize = game.plotCardCounts.find((count) => count.playerId === target)?.count ?? 0;
  const mustChoose = hasOpinionMaker && handSize > 0;
  const ready = target !== '' && (!mustChoose || choice !== null);

  const take = async () => {
    setSubmitting(true);
    try {
      await onTake(card.id, target, mustChoose ? choice : null);
      setTarget('');
      setChoice(null);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <section aria-label={name} className="w-full flex items-center gap-3 rounded-xl bg-slate-900 p-4 ring-1 ring-slate-700 shadow-lg">
      <Icon className="w-16 shrink-0" title={name} />
      <div className="min-w-0 flex-1">
        <p className="font-medium text-slate-100">{name}</p>
        <p className="mb-1 text-sm text-slate-400">{effect}</p>
        <label htmlFor="take-target" className="sr-only">
          {t('resistance.take.target')}
        </label>
        <select
          id="take-target"
          value={target}
          disabled={submitting}
          onChange={(event) => {
            setTarget(event.target.value);
            setChoice(null);
          }}
          className="w-full rounded-lg bg-slate-800 px-3 py-2 text-slate-100 ring-1 ring-slate-700"
        >
          <option value="" disabled>
            {t('resistance.take.pick')}
          </option>
          {others.map((player) => (
            <option key={player.id} value={player.id}>
              {player.name}
            </option>
          ))}
        </select>
        {mustChoose && (
          <fieldset className="mt-2 flex flex-col gap-1 text-sm text-slate-200">
            <legend className="sr-only">{t('resistance.take.which')}</legend>
            <label className="flex items-center gap-2">
              <input type="radio" name="take-choice" checked={choice === true} onChange={() => setChoice(true)} />
              {t('resistance.take.opinionMaker')}
            </label>
            <label className="flex items-center gap-2">
              <input type="radio" name="take-choice" checked={choice === false} onChange={() => setChoice(false)} />
              {t('resistance.take.random')}
            </label>
          </fieldset>
        )}
        <button
          type="button"
          disabled={!ready || submitting}
          onClick={() => void take()}
          className="mt-2 w-full rounded-lg bg-emerald-500 px-4 py-2 font-semibold text-slate-900 transition-colors hover:bg-emerald-400 disabled:cursor-not-allowed disabled:bg-slate-700 disabled:text-slate-400"
        >
          {t('resistance.take.submit')}
        </button>
      </div>
    </section>
  );
};
