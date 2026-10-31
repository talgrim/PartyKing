import { ReactNode, useState } from 'react';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { PLOT_CARDS } from './resistancePlotCards';

type Props = {
  game: ResistanceGameDto;
  onTakeLead: (cardId: number) => Promise<void>;
  onNoConfidence: (cardId: number) => Promise<void>;
  onPass: () => Promise<void>;
};

/**
 * The window before the leader acts (leader window), the one after an approved vote (confidence window) and the one after the mission cards (peek window). It opens
 * whenever anyone holds a playable card, whatever it is, so everybody sees the same thing: a question whether to play a
 * card. Only a holder of the matching card (Strong Leader, No Confidence) gets the extra button, and nothing here names
 * a card to those who do not hold it. The window ends when all confirmed or a card was played.
 */
export const ResistanceLeaderWindow = ({ game, onTakeLead, onNoConfidence, onPass }: Props): ReactNode => {
  const [submitting, setSubmitting] = useState(false);
  const confidence = game.phase === 'ConfidenceWindow';
  const type = confidence ? 'NoConfidence' : 'StrongLeader';
  const card = game.yourPlotCards.find((x) => x.type === type);
  const youLead = game.yourPlayerId === game.leaderPlayerId;
  const passed = game.yourPlayerId != null && game.leaderWindowPassedPlayerIds.includes(game.yourPlayerId);
  const { name, effect, Icon } = PLOT_CARDS[type];
  const peek = game.phase === 'PeekWindow';
  // Keeping a Close Eye is played in ResistanceCloseEye; here the peek window only asks for the confirmation
  const canPlay = !peek && card != null && (confidence || !youLead);

  const run = async (action: () => Promise<void>) => {
    setSubmitting(true);
    try {
      await action();
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <section
      aria-label="Zagrywanie kart"
      className="w-full flex items-center gap-3 rounded-xl bg-slate-900 p-4 ring-1 ring-slate-700 shadow-lg"
    >
      {canPlay && <Icon className="w-16 shrink-0" title={name} />}
      <div className="min-w-0 flex-1">
        <p className="font-medium text-slate-100">Czy zagrywasz kartę Intrygi?</p>
        <p className="mb-2 text-sm text-slate-400">
          {canPlay ? effect : 'Potwierdź, że nie zagrywasz karty. Nie widać, kto ma jakie karty.'}
        </p>
        {canPlay && (
          <button
            type="button"
            disabled={submitting}
            onClick={() => void run(() => (confidence ? onNoConfidence(card.id) : onTakeLead(card.id)))}
            className="mb-2 w-full rounded-lg bg-emerald-500 px-4 py-2 font-semibold text-slate-900 transition-colors hover:bg-emerald-400 disabled:cursor-not-allowed disabled:bg-slate-700 disabled:text-slate-400"
          >
            {confidence ? 'Odrzuć zatwierdzoną drużynę' : 'Przejmij przywództwo'}
          </button>
        )}
        <button
          type="button"
          disabled={submitting || passed}
          onClick={() => void run(onPass)}
          className="w-full rounded-lg bg-slate-700 px-4 py-2 font-semibold text-slate-100 transition-colors hover:bg-slate-600 disabled:cursor-not-allowed disabled:text-slate-400"
        >
          {passed ? 'Potwierdzono' : 'Nie zagrywam karty'}
        </button>
      </div>
    </section>
  );
};
