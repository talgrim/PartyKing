import { ReactNode } from 'react';
import { ResistancePlayerDto } from '@/service/endpoints/games/types';
import { seatPositions } from './ringoSeats';

type Props = {
  players: ResistancePlayerDto[];
  yourPlayerId: string | null;
};

/** The players who joined, seated along the left, bottom and right edges in seating order. */
export const ResistancePlayers = ({ players, yourPlayerId }: Props): ReactNode => {
  const seats = seatPositions(players.length);

  return (
    <ul aria-label="Players" className="absolute inset-0 z-30 pointer-events-none">
      {players.map((player, index) => {
        const you = player.id === yourPlayerId;

        return (
          <li
            key={player.id}
            title={player.name}
            style={{ left: `${seats[index].left}%`, top: `${seats[index].top}%` }}
            className="absolute -translate-x-1/2 -translate-y-1/2 max-w-[9vw] truncate px-2 py-1 rounded-md text-sm bg-slate-900 text-slate-200"
          >
            {player.name}
            {you && <span className="sr-only"> (you)</span>}
            {you && <span aria-hidden="true">*</span>}
          </li>
        );
      })}
    </ul>
  );
};
