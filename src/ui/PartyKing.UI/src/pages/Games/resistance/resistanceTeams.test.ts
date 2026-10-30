import { ResistanceRulesDto } from '@/service/endpoints/games/types';
import { missionBoardFor } from './resistanceTeams';

const rules: ResistanceRulesDto = {
  playerCounts: [
    { playerCount: 5, spies: 2, teamSizes: [2, 3, 2, 3, 3], requiredFails: [1, 1, 1, 1, 1] },
    { playerCount: 6, spies: 2, teamSizes: [2, 3, 4, 3, 4], requiredFails: [1, 1, 1, 1, 1] },
    { playerCount: 7, spies: 3, teamSizes: [2, 3, 3, 4, 4], requiredFails: [1, 1, 1, 2, 1] },
    { playerCount: 10, spies: 4, teamSizes: [3, 4, 4, 5, 5], requiredFails: [1, 1, 1, 2, 1] },
  ],
  plotCardsEnabled: false,
  plotCardsPerRound: 1,
};

describe('missionBoardFor', () => {
  it.each([
    [5, [2, 3, 2, 3, 3], false],
    [6, [2, 3, 4, 3, 4], false],
    [7, [2, 3, 3, 4, 4], true],
    [10, [3, 4, 4, 5, 5], true],
  ])('takes the team sizes for %i players from the rules', (count, sizes, twoFails) => {
    const board = missionBoardFor(count, rules);

    expect(board.teamSizes).toEqual(sizes);
    expect(board.fourthMissionNeedsTwoFails).toBe(twoFails);
    expect(board.provisional).toBe(false);
  });

  it.each([0, 1, 4])('previews the 5-player sizes below 5 players (%i)', (count) => {
    const board = missionBoardFor(count, rules);

    expect(board.teamSizes).toEqual([2, 3, 2, 3, 3]);
    expect(board.provisional).toBe(true);
  });

  it('follows a changed configuration', () => {
    const changed = { ...rules, playerCounts: [{ ...rules.playerCounts[0], requiredFails: [1, 1, 1, 2, 1] }] };

    expect(missionBoardFor(5, changed).fourthMissionNeedsTwoFails).toBe(true);
  });

  it('shows no sizes until the rules are loaded', () => {
    expect(missionBoardFor(5).teamSizes).toEqual([0, 0, 0, 0, 0]);
  });
});
