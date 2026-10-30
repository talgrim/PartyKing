import { ReactNode } from 'react';
import { LeaderTokenIcon } from '@/pages/Games/resistance/ResistanceIcons';
import { ResistancePlayerDto } from '@/service/endpoints/games/types';
import { seatPositions } from './ringoSeats';

type Props = {
  players: ResistancePlayerDto[];
  yourPlayerId: string | null;
  /** The player leading the mission, who gets the crown token; none before the start. */
  leaderId?: string | null;
  /** Extra badges after a player's name (votes, roles). */
  marks?: (playerId: string) => ReactNode;
  /** A card shown by a player's tag (face-up plot cards): above it along the bottom, below it along the side margins, so it stays out of the board. */
  beside?: (playerId: string) => ReactNode;
};

/** The players who joined, seated along the left, bottom and right edges in seating order. */
export const ResistancePlayers = ({ players, yourPlayerId, leaderId = null, marks, beside }: Props): ReactNode => {
  const seats = seatPositions(players.length);

  return (
    <ul aria-label="Players" className="absolute inset-0 z-30 pointer-events-none">
      {players.map((player, index) => {
        const you = player.id === yourPlayerId;
        const leader = player.id === leaderId;
        const seat = seats[index];
        const side = seat.top >= 90 ? 'bottom-full mb-1 left-1/2 -translate-x-1/2' : 'top-full mt-1 left-1/2 -translate-x-1/2';
        const card = beside?.(player.id);

        return (
          <li
            key={player.id}
            title={player.name}
            style={{ left: `${seats[index].left}%`, top: `${seats[index].top}%` }}
            className={`absolute -translate-x-1/2 -translate-y-1/2 max-w-[11vw] flex items-center gap-[0.4vw] px-[max(0.5rem,0.6vw)] py-[max(0.25rem,0.3vw)] rounded-md text-[clamp(0.875rem,1.5vw,2.25rem)] ${
              you
                ? 'bg-amber-400 text-slate-900 font-semibold ring-2 ring-amber-200 shadow-lg shadow-amber-400/40'
                : 'bg-slate-900 text-slate-200'
            }`}
          >
            {leader && <LeaderTokenIcon title="Leader" />}
            <span className="truncate">
              {player.name}
              {you && <span className="sr-only"> (you)</span>}
            </span>
            {you && (
              <span aria-hidden="true" className="shrink-0 text-[0.8em] uppercase rounded bg-slate-900 text-amber-300 px-1">
                Ty
              </span>
            )}
            {marks?.(player.id)}
            {card && <span className={`absolute ${side} w-[6vw] min-w-12`}>{card}</span>}
          </li>
        );
      })}
    </ul>
  );
};
