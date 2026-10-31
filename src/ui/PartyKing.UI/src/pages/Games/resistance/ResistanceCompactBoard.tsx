import { ReactNode } from 'react';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { LeaderTokenIcon } from './ResistanceIcons';
import { ResistanceMissionBoard } from './ResistanceMissionBoard';
import { ResistancePlayerMarks } from './ResistancePlayerMarks';
import { MissionBoardLayout } from './resistanceTeams';
import { useTranslation } from 'react-i18next';

type Props = {
  game: ResistanceGameDto | undefined;
  board: MissionBoardLayout;
};

/** The board for a phone: the mission board across the full width, the players as a list below it. */
export const ResistanceCompactBoard = ({ game, board }: Props): ReactNode => {
  const { t } = useTranslation();

  return (
  <div className="flex flex-col gap-3">
    <ResistanceMissionBoard
      board={board}
      currentMission={game?.startedAt != null ? game.missionNumber - 1 : null}
      rejectedTeams={game?.rejectedTeams}
      missionResults={game?.missionResults}
      players={game?.players}
    />

    {game && game.players.length > 0 && (
      <ul aria-label={t('lobby.players')} className="flex flex-col gap-1">
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
              {player.id === game.leaderPlayerId && <LeaderTokenIcon title={t('resistance.leader')} />}
              <span className="truncate">
                {player.name}
                {you && <span className="sr-only"> {t('lobby.you')}</span>}
              </span>
              <span className="ml-auto flex shrink-0 items-center gap-1">
                <ResistancePlayerMarks game={game} playerId={player.id} />
              </span>
              {onTeam && (
                <span className="shrink-0 text-xs uppercase rounded bg-emerald-400 text-slate-900 px-1">
                  {t('resistance.board.onTeam')}
                </span>
              )}
              {you && (
                <span aria-hidden="true" className={`shrink-0 text-xs uppercase rounded bg-slate-900 text-amber-300 px-1`}>
                  {t('resistance.youBadge')}
                </span>
              )}
            </li>
          );
        })}
      </ul>
    )}
  </div>
  );
};
