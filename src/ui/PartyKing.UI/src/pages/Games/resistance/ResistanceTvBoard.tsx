import { ReactNode } from 'react';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { ResistancePlayers } from '../ResistancePlayers';
import { ResistanceMissionBoard } from './ResistanceMissionBoard';
import { ResistanceMissionCards } from './ResistanceMissionCards';
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

  // The votes show from the moment all voted until the next leader submits a team (after a rejection) or the mission
  // is over. The server only sends them while they are public.
  const votesShown = game?.phase === 'SelectingTeam' || game?.phase === 'PlayingMission';
  const votes = votesShown && game?.votes && game.votes.length > 0 ? game.votes : null;

  // The cards of the mission that just finished, until "Dalej" starts the next one (also when it ended the game).
  const finished = game?.phase === 'MissionResult' || game?.phase === 'GameOver'
    ? game.missionResults.find((result) => result.missionNumber === game.missionNumber)
    : undefined;
  const cards = finished && team.length > 0 ? { count: team.length, fails: finished.failCount } : null;

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
            missionResults={game?.missionResults}
          />
        </div>
        {(team.length > 0 || votes || cards) && (
          <div className="flex w-full shrink-0 items-start justify-center gap-[4vw]">
            {team.length > 0 && (
              <ResistanceTeamCards members={team} missionNumber={game!.missionNumber} leaderId={game!.leaderPlayerId} />
            )}
            {votes && <ResistanceVoteCards players={game!.players} votes={votes} />}
            {cards && <ResistanceMissionCards cardCount={cards.count} failCount={cards.fails} />}
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
