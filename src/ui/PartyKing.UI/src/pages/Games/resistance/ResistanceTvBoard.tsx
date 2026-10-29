import { ReactNode } from 'react';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { ResistancePlayers } from '../ResistancePlayers';
import { ResistanceMissionBoard } from './ResistanceMissionBoard';
import { ResistancePlayerMarks } from './ResistancePlayerMarks';
import { ResistanceTeamCards } from './ResistanceTeamCards';
import { ResistanceVoteCards } from './ResistanceVoteCards';
import { MissionBoardLayout } from './resistanceTeams';

type Props = {
  game: ResistanceGameDto | undefined;
  board: MissionBoardLayout;
  banner: ReactNode;
  /** Marks the viewer's own seat; the TV tab shows no one as "you". */
  yourPlayerId: string | null;
};

/** The board for a big screen: the board and the phase line keep to the middle 80%, the players sit in the margins. */
export const ResistanceTvBoard = ({ game, board, banner, yourPlayerId }: Props): ReactNode => {
  const team = game?.players.filter((player) => game.teamPlayerIds.includes(player.id)) ?? [];

  const votes = game?.votes && game.votes.length > 0 ? game.votes : null;

  return (
    <>
      <div className="absolute inset-[10%] flex flex-col items-center gap-4 overflow-hidden">
        {banner}
        <div className="relative min-h-0 w-full flex-1">
          <ResistanceMissionBoard
            board={board}
            className="absolute inset-0 h-full w-full"
            currentMission={game?.startedAt != null ? game.missionNumber - 1 : null}
            rejectedTeams={game?.rejectedTeams}
          />
        </div>
        {(team.length > 0 || votes) && (
          <div className="flex w-full shrink-0 items-start justify-center gap-[4vw]">
            {team.length > 0 && (
              <ResistanceTeamCards members={team} missionNumber={game!.missionNumber} leaderId={game!.leaderPlayerId} />
            )}
            {votes && <ResistanceVoteCards players={game!.players} votes={votes} />}
          </div>
        )}
      </div>

      {game && game.players.length > 0 && (
        <ResistancePlayers
          players={game.players}
          yourPlayerId={yourPlayerId}
          leaderId={game.leaderPlayerId}
          marks={(playerId) => <ResistancePlayerMarks game={game} playerId={playerId} />}
        />
      )}
    </>
  );
};
