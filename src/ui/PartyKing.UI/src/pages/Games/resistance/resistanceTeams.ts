import { ResistanceRulesDto } from '@/service/endpoints/games/types';

export const MIN_PLAYERS = 5;
export const MAX_PLAYERS = 10;

// Shown only until the game is loaded
const NO_SIZES: readonly number[] = [0, 0, 0, 0, 0];

/**
 * The mission board for the players who joined so far, from the rules the server created the game with. Below 5
 * players the 5-player sizes are shown as a preview (`provisional`); above 10 the 10-player sizes (the lobby does not
 * allow that many).
 */
export const missionBoardFor = (playerCount: number, rules?: ResistanceRulesDto) => {
  const count = Math.min(Math.max(playerCount, MIN_PLAYERS), MAX_PLAYERS);
  const forCount = rules?.playerCounts.find((x) => x.playerCount === count);

  return {
    teamSizes: forCount?.teamSizes ?? NO_SIZES,
    provisional: playerCount < MIN_PLAYERS,
    // Mission 4 may need more than one Fail card, depending on the configuration
    fourthMissionNeedsTwoFails: (forCount?.requiredFails[3] ?? 1) >= 2,
  };
};

export type MissionBoardLayout = ReturnType<typeof missionBoardFor>;
