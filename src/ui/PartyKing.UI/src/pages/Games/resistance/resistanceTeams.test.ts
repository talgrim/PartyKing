import { missionBoardFor } from './resistanceTeams';

describe('missionBoardFor', () => {
  it.each([
    [5, [2, 3, 2, 3, 3], false],
    [6, [2, 3, 4, 3, 4], false],
    [7, [2, 3, 3, 4, 4], true],
    [8, [3, 4, 4, 5, 5], true],
    [10, [3, 4, 4, 5, 5], true],
  ])('gives the team sizes for %i players', (count, sizes, twoFails) => {
    const board = missionBoardFor(count);

    expect(board.teamSizes).toEqual(sizes);
    expect(board.fourthMissionNeedsTwoFails).toBe(twoFails);
    expect(board.provisional).toBe(false);
  });

  it.each([0, 1, 4])('previews the 5-player sizes below 5 players (%i)', (count) => {
    const board = missionBoardFor(count);

    expect(board.teamSizes).toEqual([2, 3, 2, 3, 3]);
    expect(board.provisional).toBe(true);
  });
});
