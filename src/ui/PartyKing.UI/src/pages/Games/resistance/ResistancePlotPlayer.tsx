import { ReactNode, useState } from 'react';
import { ResistanceGameDto, ResistancePlotCardDto, ResistanceRole } from '@/service/endpoints/games/types';
import { IMMEDIATE_PLOT_CARDS, PLOT_CARDS } from './resistancePlotCards';

type Props = {
  game: ResistanceGameDto;
  onPlay: (cardId: number, targetPlayerId: string) => Promise<void>;
  onConfirm: (cardId: number) => Promise<void>;
};

const ROLE_NAMES: Record<ResistanceRole, string> = { Resistance: 'Ruch Oporu', Spy: 'Szpieg' };

/**
 * The player's part of the immediate plot cards: each of their unplayed cards needs a target, and every identity card
 * shown to them has to be confirmed. Nothing can be skipped: the game waits for all of it.
 */
export const ResistancePlotPlayer = ({ game, onPlay, onConfirm }: Props): ReactNode => {
  const toPlay = game.yourPlotCards.filter((card) => IMMEDIATE_PLOT_CARDS.includes(card.type) && !card.toDeal);
  const nameOf = (id: string) => game.players.find((player) => player.id === id)?.name ?? 'gracz';

  if (toPlay.length === 0 && game.yourReveals.length === 0) return null;

  return (
    <section aria-label="Karty natychmiastowe" className="w-full flex flex-col gap-3 rounded-xl bg-slate-900 p-4 ring-1 ring-slate-700 shadow-lg">
      <h2 className="text-lg font-semibold text-slate-100">Karty natychmiastowe</h2>

      {toPlay.map((card) => (
        <PlayCard key={card.id} card={card} game={game} onPlay={onPlay} />
      ))}

      {game.yourReveals.map((reveal) => (
        <div key={reveal.cardId} className="flex flex-col gap-2 rounded-lg bg-slate-800 p-3">
          <p className="text-slate-100">
            Karta Tożsamości gracza <strong>{nameOf(reveal.playerId)}</strong>:{' '}
            <strong className={reveal.role === 'Spy' ? 'text-red-400' : 'text-sky-300'}>{ROLE_NAMES[reveal.role]}</strong>.
          </p>
          <button
            type="button"
            onClick={() => void onConfirm(reveal.cardId)}
            className="rounded-lg bg-emerald-500 px-4 py-2 font-semibold text-slate-900 transition-colors hover:bg-emerald-400"
          >
            Widziałem/am
          </button>
        </div>
      ))}
    </section>
  );
};

type PlayCardProps = {
  card: ResistancePlotCardDto;
  game: ResistanceGameDto;
  onPlay: (cardId: number, targetPlayerId: string) => Promise<void>;
};

const PlayCard = ({ card, game, onPlay }: PlayCardProps): ReactNode => {
  const [target, setTarget] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const { name, effect, Icon } = PLOT_CARDS[card.type];

  const me = game.players.findIndex((player) => player.id === game.yourPlayerId);
  const count = game.players.length;
  const options =
    card.type === 'OverheardConversation'
      ? [game.players[(me + count - 1) % count], game.players[(me + 1) % count]]
      : game.players.filter((player) => player.id !== game.yourPlayerId);
  const selectId = `plot-target-${card.id}`;

  const play = async () => {
    setSubmitting(true);
    try {
      await onPlay(card.id, target);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="flex items-center gap-3">
      <Icon className="w-16 shrink-0" title={name} />
      <div className="min-w-0 flex-1">
        <p className="font-medium text-slate-100">{name}</p>
        <p className="mb-1 text-sm text-slate-400">{effect}</p>
        <label htmlFor={selectId} className="sr-only">
          Na kogo zagrywasz kartę „{name}”
        </label>
        <select
          id={selectId}
          value={target}
          disabled={submitting}
          onChange={(event) => setTarget(event.target.value)}
          className="w-full rounded-lg bg-slate-800 px-3 py-2 text-slate-100 ring-1 ring-slate-700"
        >
          <option value="" disabled>
            Wybierz gracza
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
          onClick={() => void play()}
          className="mt-2 w-full rounded-lg bg-emerald-500 px-4 py-2 font-semibold text-slate-900 transition-colors hover:bg-emerald-400 disabled:cursor-not-allowed disabled:bg-slate-700 disabled:text-slate-400"
        >
          Zagraj kartę
        </button>
      </div>
    </div>
  );
};
