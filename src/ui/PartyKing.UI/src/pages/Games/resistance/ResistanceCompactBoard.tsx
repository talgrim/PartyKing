import { ReactNode } from 'react';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { LeaderTokenIcon } from './ResistanceIcons';
import { ResistanceMissionBoard } from './ResistanceMissionBoard';
import { MissionBoardLayout } from './resistanceTeams';

type Props = {
  game: ResistanceGameDto | undefined;
  board: MissionBoardLayout;
};

/** The board for a phone: the mission board across the full width, the players as a list below it. */
export const ResistanceCompactBoard = ({ game, board }: Props): ReactNode => (
  <div className="flex flex-col gap-3">
    <ResistanceMissionBoard
      board={board}
      className="w-full"
      currentMission={game?.startedAt != null ? game.missionNumber - 1 : null}
    />

    {game && game.players.length > 0 && (
      <ul aria-label="Players" className="flex flex-col gap-1">
        {game.players.map((player) => {
          const you = player.id === game.yourPlayerId;
          const onTeam = game.teamPlayerIds.includes(player.id);

          return (
            <li
              key={player.id}
              className={`flex items-center gap-2 px-3 py-2 rounded-md ${
                you ? 'bg-amber-400 text-slate-900 font-semibold' : 'bg-slate-900 text-slate-200'
              } ${onTeam ? 'ring-2 ring-emerald-400' : ''}`}
            >
              {player.id === game.leaderPlayerId && <LeaderTokenIcon className="w-5 h-5 shrink-0" title="Leader" />}
              <span className="truncate">
                {player.name}
                {you && <span className="sr-only"> (you)</span>}
              </span>
              {onTeam && (
                <span className="ml-auto shrink-0 text-xs uppercase rounded bg-emerald-400 text-slate-900 px-1">
                  W drużynie
                </span>
              )}
              {you && (
                <span aria-hidden="true" className={`${onTeam ? '' : 'ml-auto '} shrink-0 text-xs uppercase rounded bg-slate-900 text-amber-300 px-1`}>
                  Ty
                </span>
              )}
            </li>
          );
        })}
      </ul>
    )}
  </div>
);
