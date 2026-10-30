import { ReactNode, useState } from 'react';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { PLOT_CARDS } from './resistancePlotCards';

type Props = {
  game: ResistanceGameDto;
  onTakeLead: (cardId: number) => Promise<void>;
  onPass: () => Promise<void>;
};

/**
 * Leader window: a Strong Leader is in someone's hand, so everyone confirms they play no card (nobody can tell who
 * holds one) and the holder may take the lead instead. The window ends when all confirmed or the lead was taken.
 */
export const ResistanceLeaderWindow = ({ game, onTakeLead, onPass }: Props): ReactNode => {
  const [submitting, setSubmitting] = useState(false);
  const card = game.yourPlotCards.find((x) => x.type === 'StrongLeader');
  const youLead = game.yourPlayerId === game.leaderPlayerId;
  const passed = game.yourPlayerId != null && game.leaderWindowPassedPlayerIds.includes(game.yourPlayerId);
  const { name, effect, Icon } = PLOT_CARDS.StrongLeader;

  const run = async (action: () => Promise<void>) => {
    setSubmitting(true);
    try {
      await action();
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <section aria-label={name} className="w-full flex items-center gap-3 rounded-xl bg-slate-900 p-4 ring-1 ring-slate-700 shadow-lg">
      <Icon className="w-16 shrink-0" title={name} />
      <div className="min-w-0 flex-1">
        <p className="font-medium text-slate-100">{name}</p>
        <p className="mb-2 text-sm text-slate-400">{effect}</p>
        {card != null && !youLead && (
          <button
            type="button"
            disabled={submitting}
            onClick={() => void run(() => onTakeLead(card.id))}
            className="mb-2 w-full rounded-lg bg-emerald-500 px-4 py-2 font-semibold text-slate-900 transition-colors hover:bg-emerald-400 disabled:cursor-not-allowed disabled:bg-slate-700 disabled:text-slate-400"
          >
            Przejmij przywództwo
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
