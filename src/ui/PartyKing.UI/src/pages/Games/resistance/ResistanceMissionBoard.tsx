import { ReactNode } from 'react';
import { MissionBoardIcon, MissionResult } from './ResistanceIcons';
import { MissionBoardLayout } from './resistanceTeams';

const NO_RESULTS: readonly MissionResult[] = [null, null, null, null, null];

type Props = {
  board: MissionBoardLayout;
  className?: string;
};

/** The mission board of the current game; no mission is played yet, so it shows no results. */
export const ResistanceMissionBoard = ({ board, className }: Props): ReactNode => (
  <MissionBoardIcon
    className={className}
    title="Mission board"
    teamSizes={board.teamSizes}
    results={NO_RESULTS}
    currentMission={null}
    rejectedTeams={0}
    fourthMissionNeedsTwoFails={board.fourthMissionNeedsTwoFails}
    dimmed={board.provisional}
  />
);
