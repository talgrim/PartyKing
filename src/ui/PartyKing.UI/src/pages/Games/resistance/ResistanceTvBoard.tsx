import { ReactNode } from 'react';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { ResistancePlayers } from '../ResistancePlayers';
import { ResistanceMissionBoard } from './ResistanceMissionBoard';
import { ResistanceMissionCards } from './ResistanceMissionCards';
import { PlotOpinionMakerCardIcon } from './ResistanceIcons';
import { ResistancePlayerMarks } from './ResistancePlayerMarks';
import { ResistanceTeamCards } from './ResistanceTeamCards';
import { ResistanceVoteCards } from './ResistanceVoteCards';
import { MissionBoardLayout } from './resistanceTeams';
import { useTranslation } from 'react-i18next';

type Props = {
  game: ResistanceGameDto | undefined;
  board: MissionBoardLayout;
  banner: ReactNode;
  /** Marks the viewer's own seat; the TV tab shows no one as "you". */
  yourPlayerId: string | null;
  /** The players connected right now; undefined until the first answer, which shows no status at all. */
  onlinePlayerIds?: string[];
};

/** The board for a big screen: the board and the phase line keep to the middle 80%, the players sit in the margins. */
export const ResistanceTvBoard = ({ game, board, banner, yourPlayerId, onlinePlayerIds }: Props): ReactNode => {
  const { t } = useTranslation();
  const team = game?.players.filter((player) => game.teamPlayerIds.includes(player.id)) ?? [];

  // The votes show from the moment all voted until the next leader submits a team (after a rejection) or the mission
  // is over. The server only sends them while they are public.
  const votesShown = game?.phase === 'SelectingTeam' || game?.phase === 'PlayingMission';
  const allVotes = votesShown && game?.votes && game.votes.length > 0 ? game.votes : null;
  // While the others are still voting, the Opinion Makers' final votes are already public.
  const earlyVotes = game?.phase === 'Voting' && game.opinionMakerVotes.length > 0 ? game.opinionMakerVotes : null;
  const votes = allVotes ?? earlyVotes;

  // The cards of the mission that just finished, until "Next" starts the next one (also when it ended the game).
  // The number only advances on "Next", so a result for the current mission exists exactly until then, in whatever phase.
  const finished = game?.missionResults.find((result) => result.missionNumber === game.missionNumber);
  const openPlayer = game?.players.find((player) => player.id === game.openMissionPlayerId);
  const open = openPlayer && game?.openMissionCard != null ? { name: openPlayer.name, card: game.openMissionCard } : null;
  const cards = finished && team.length > 0 ? { count: team.length, fails: finished.failCount } : null;
  // While the mission is played the openly played card is already public, the rest stay hidden until the mission ends
  const showCards = cards ?? (game?.phase === 'PlayingMission' && open ? { count: 1, fails: open.card === 'Success' ? 0 : 1 } : null);

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
            players={game?.players}
          />
        </div>
        {(team.length > 0 || votes || showCards) && (
          <div className="flex w-full shrink-0 flex-wrap items-stretch justify-center gap-3">
            {team.length > 0 && (
              <ResistanceTeamCards members={team} missionNumber={game!.missionNumber} leaderId={game!.leaderPlayerId} />
            )}
            {votes && <ResistanceVoteCards players={game!.players} votes={votes} />}
            {showCards && <ResistanceMissionCards cardCount={showCards.count} failCount={showCards.fails} open={open} />}
          </div>
        )}
      </div>

      {game && game.players.length > 0 && (
        <ResistancePlayers
          players={game.players}
          yourPlayerId={yourPlayerId}
          leaderId={game.leaderPlayerId}
          onlinePlayerIds={onlinePlayerIds}
          marks={(playerId) => <ResistancePlayerMarks game={game} playerId={playerId} />}
          beside={(playerId) =>
            game.opinionMakerPlayerIds.includes(playerId) ? (
              <PlotOpinionMakerCardIcon className="w-full" title={t('resistance.marks.opinionMaker')} />
            ) : null
          }
        />
      )}
    </>
  );
};
