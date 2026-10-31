import { ReactNode } from 'react';
import { MissionBoardIcon, MissionResult } from './ResistanceIcons';
import { ResistanceMissionResultDto, ResistancePlayerDto } from '@/service/endpoints/games/types';
import { MissionBoardLayout } from './resistanceTeams';

type Props = {
  board: MissionBoardLayout;
  className?: string;
  /** Index (0-4) of the mission being played; none before the start. */
  currentMission?: number | null;
  /** Teams rejected in a row during the mission (0-5). */
  rejectedTeams?: number;
  /** The finished missions. */
  missionResults?: readonly ResistanceMissionResultDto[];
  /** The players of the game, to name each finished mission's team. */
  players?: readonly Pick<ResistancePlayerDto, 'id' | 'name'>[];
};

/** The mission board of the current game with the result of every finished mission. */
export const ResistanceMissionBoard = ({ board, className, currentMission = null, rejectedTeams = 0, missionResults = [], players = [] }: Props): ReactNode => (
  <MissionBoardIcon
    className={className}
    title="Mission board"
    teamSizes={board.teamSizes}
    results={[0, 1, 2, 3, 4].map((i): MissionResult => {
      const result = missionResults.find((x) => x.missionNumber === i + 1);

      return result == null ? null : result.succeeded ? 'success' : 'fail';
    })}
    teams={[0, 1, 2, 3, 4].map((i) => {
      const ids = missionResults.find((x) => x.missionNumber === i + 1)?.teamPlayerIds ?? [];

      return ids.flatMap((id) => players.find((p) => p.id === id)?.name ?? []);
    })}
    currentMission={currentMission}
    rejectedTeams={rejectedTeams}
    fourthMissionNeedsTwoFails={board.fourthMissionNeedsTwoFails}
    dimmed={board.provisional}
  />
);
