import { ReactNode, useState } from 'react';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { VoteApproveCardIcon, VoteRejectCardIcon } from './ResistanceIcons';

type Props = {
  game: ResistanceGameDto;
  onVote: (approve: boolean) => Promise<void>;
};

const card = (selected: boolean, tone: 'emerald' | 'rose', disabled: boolean) =>
  `h-[clamp(10rem,40vw,20rem)] w-auto flex-none rounded-xl transition ${disabled ? 'cursor-not-allowed' : 'cursor-pointer'} ${
    selected
      ? `ring-4 ${tone === 'emerald' ? 'ring-emerald-400' : 'ring-rose-400'}`
      : 'opacity-70 hover:opacity-100'
  }`;

/**
 * The player's vote on the submitted team. The choice can be changed until the last player has voted, so the buttons
 * stay active and show which one is picked.
 */
export const ResistanceVotePanel = ({ game, onVote }: Props): ReactNode => {
  const [sending, setSending] = useState(false);
  const voted = game.yourVote != null;
  const makers = game.opinionMakerPlayerIds;
  const youAreMaker = game.yourPlayerId != null && makers.includes(game.yourPlayerId);
  // An Opinion Maker's vote is final, and the others wait until every Opinion Maker has revealed theirs
  const locked = youAreMaker && voted;
  const waitingFor = youAreMaker
    ? []
    : game.players.filter((player) => makers.includes(player.id) && !game.votedPlayerIds.includes(player.id));
  const blocked = locked || waitingFor.length > 0;

  const vote = async (approve: boolean) => {
    setSending(true);
    try {
      await onVote(approve);
    } finally {
      setSending(false);
    }
  };

  return (
    <section aria-label="Głosowanie" className="w-full flex flex-col gap-3 rounded-xl bg-slate-900 p-4 ring-1 ring-slate-700 shadow-lg">
      <div className="flex items-baseline justify-between gap-2">
        <h2 className="text-lg font-semibold text-slate-100">Głosowanie nad drużyną</h2>
        <span className="text-sm tabular-nums text-slate-400" aria-live="polite">
          {game.votedPlayerIds.length}/{game.players.length}
        </span>
      </div>
      <div className="flex justify-center gap-4">
        <VoteApproveCardIcon
          title="Zatwierdź"
          className={card(game.yourVote === true, 'emerald', sending || blocked)}
          pressed={game.yourVote === true}
          disabled={sending || blocked}
          onClick={() => void vote(true)}
        />
        <VoteRejectCardIcon
          title="Odrzuć"
          className={card(game.yourVote === false, 'rose', sending || blocked)}
          pressed={game.yourVote === false}
          disabled={sending || blocked}
          onClick={() => void vote(false)}
        />
      </div>
      <p className="text-sm text-slate-400">
        {waitingFor.length > 0
          ? `Najpierw głosuje kreator opinii: ${waitingFor.map((player) => player.name).join(', ')}.`
          : locked
            ? 'Twój głos jako kreatora opinii jest jawny i nie można go zmienić.'
            : voted
              ? 'Twój głos jest zapisany. Możesz go zmienić, dopóki nie zagłosują wszyscy.'
              : youAreMaker
                ? 'Jako kreator opinii głosujesz pierwszy, a twój głos jest jawny i ostateczny.'
                : 'Głosy są jawne dopiero wtedy, gdy zagłosują wszyscy.'}
      </p>
    </section>
  );
};
