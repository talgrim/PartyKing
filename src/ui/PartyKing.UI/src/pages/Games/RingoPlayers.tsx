import { ReactNode } from 'react';
import { RingoPlayerDto } from '@/service/endpoints/games/types';
import { seatPositions } from './ringoSeats';
import { useTranslation } from 'react-i18next';

type Props = {
  players: RingoPlayerDto[];
  currentPlayerId: string | null;
  yourPlayerId: string | null;
};

/** The players, seated along the left, bottom and right edges in turn order; the one on turn is highlighted. */
export const RingoPlayers = ({ players, currentPlayerId, yourPlayerId }: Props): ReactNode => {
  const { t } = useTranslation();
  const seats = seatPositions(players.length);

  return (
    <ul aria-label={t('lobby.players')} className="absolute inset-0 z-30 pointer-events-none">
      {players.map((player, index) => {
        const onTurn = player.id === currentPlayerId;
        const you = player.id === yourPlayerId;

        return (
          <li
            key={player.id}
            aria-current={onTurn ? 'true' : undefined}
            title={player.name}
            style={{ left: `${seats[index].left}%`, top: `${seats[index].top}%` }}
            className={`absolute -translate-x-1/2 -translate-y-1/2 max-w-[9vw] truncate px-2 py-1 rounded-md text-sm ${
              onTurn ? 'bg-amber-500 text-slate-900 font-semibold' : 'bg-slate-900 text-slate-200'
            }`}
          >
            {player.name}
            {you && <span className="sr-only"> {t('lobby.you')}</span>}
            {you && <span aria-hidden="true">*</span>}
          </li>
        );
      })}
    </ul>
  );
};
