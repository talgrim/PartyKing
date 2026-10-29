export const MIN_PLAYERS = 5;
export const MAX_PLAYERS = 10;

// Team size of each of the 5 missions by player count (rules in CLAUDE.local.md)
const TEAM_SIZES: Record<number, readonly number[]> = {
  5: [2, 3, 2, 3, 3],
  6: [2, 3, 4, 3, 4],
  7: [2, 3, 3, 4, 4],
  8: [3, 4, 4, 5, 5],
  9: [3, 4, 4, 5, 5],
  10: [3, 4, 4, 5, 5],
};

/**
 * The mission board for the players who joined so far. Below 5 players the 5-player sizes are shown as a preview
 * (`provisional`); above 10 the 10-player sizes (the lobby does not allow that many).
 */
export const missionBoardFor = (playerCount: number) => {
  const count = Math.min(Math.max(playerCount, MIN_PLAYERS), MAX_PLAYERS);

  return {
    teamSizes: TEAM_SIZES[count],
    provisional: playerCount < MIN_PLAYERS,
    // Mission 4 needs two Fail cards from 7 players
    fourthMissionNeedsTwoFails: count >= 7,
  };
};

export type MissionBoardLayout = ReturnType<typeof missionBoardFor>;
