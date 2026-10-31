import { ReactNode, useState } from 'react';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { PLOT_CARDS } from './resistancePlotCards';

type Props = {
  game: ResistanceGameDto;
  onPlay: (cardId: number, targetPlayerId: string) => Promise<void>;
};

/**
 * In the Spotlight: in the confidence window after an approved vote (before the mission cards) its holder forces another team member to play their mission
 * card openly. The card is announced before the target plays; one player is forced per mission.
 */
export const ResistanceSpotlightPlayer = ({ game, onPlay }: Props): ReactNode => {
  const [target, setTarget] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const card = game.yourPlotCards.find((x) => x.type === 'InTheSpotlight');
  const options = game.players.filter(
    (player) =>
      player.id !== game.yourPlayerId &&
      game.teamPlayerIds.includes(player.id) &&
      !game.playedMissionPlayerIds.includes(player.id),
  );

  if (card == null || game.openMissionPlayerId != null || options.length === 0) return null;

  const { name, effect, Icon } = PLOT_CARDS.InTheSpotlight;
  const play = async () => {
    setSubmitting(true);
    try {
      await onPlay(card.id, target);
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
        <p className="mb-1 text-sm text-slate-400">{effect}</p>
        <label htmlFor="spotlight-target" className="sr-only">
          Kogo zmuszasz do jawnej karty misji
        </label>
        <select
          id="spotlight-target"
          value={target}
          disabled={submitting}
          onChange={(event) => setTarget(event.target.value)}
          className="w-full rounded-lg bg-slate-800 px-3 py-2 text-slate-100 ring-1 ring-slate-700"
        >
          <option value="" disabled>
            Wybierz gracza z drużyny
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
    </section>
  );
};
