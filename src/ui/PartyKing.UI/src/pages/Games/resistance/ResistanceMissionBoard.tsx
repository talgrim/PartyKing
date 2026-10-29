import { ReactNode } from 'react';
import { MissionBoardIcon, MissionResult } from './ResistanceIcons';
import { MissionBoardLayout } from './resistanceTeams';

const NO_RESULTS: readonly MissionResult[] = [null, null, null, null, null];

type Props = {
  board: MissionBoardLayout;
  className?: string;
  /** Index (0-4) of the mission being played; none before the start. */
  currentMission?: number | null;
  /** Teams rejected in a row during the mission (0-5). */
  rejectedTeams?: number;
};

/** The mission board of the current game; no mission is finished yet, so it shows no results. */
export const ResistanceMissionBoard = ({ board, className, currentMission = null, rejectedTeams = 0 }: Props): ReactNode => (
  <MissionBoardIcon
    className={className}
    title="Mission board"
    teamSizes={board.teamSizes}
    results={NO_RESULTS}
    currentMission={currentMission}
    rejectedTeams={rejectedTeams}
    fourthMissionNeedsTwoFails={board.fourthMissionNeedsTwoFails}
    dimmed={board.provisional}
  />
);
