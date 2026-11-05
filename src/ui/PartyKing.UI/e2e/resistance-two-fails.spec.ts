import {
  continueMission,
  playMission,
  playMissionCards,
  ResistanceGameSetup,
  spyNames,
  startResistanceGame,
  submitTeam,
  viewOf,
  voteAll,
  waitForLeader,
  waitForPhase,
} from './fixtures/resistance';
import { expect, test } from './fixtures/test';

// With seven or more players the fourth mission only fails from two Fail cards. Three spies sit among the seven, so the
// team of four can hold one or two of them.

/**
 * Plays the first three missions with the given outcomes (two of one kind, one of the other) so that nobody has won yet
 * and the fourth is next.
 */
async function reachFourthMission(
  setup: ResistanceGameSetup,
  outcomes: ['success' | 'fail', 'success' | 'fail', 'success' | 'fail'],
) {
  for (const outcome of outcomes) {
    await playMission(setup, outcome);
  }

  const [, leader] = await waitForLeader(setup);
  const view = await viewOf(leader, setup.session);
  expect(view.missionNumber).toBe(4);
  expect(view.requiredFails).toBe(2);
  const spies = await spyNames(setup);
  const resistance = [...setup.guests.keys()].filter((name) => !spies.includes(name));
  return { leader, view, spies, resistance };
}

test('a single Fail card does not fail the fourth mission, and the fifth fails from one', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest }, { players: 7 });
  // Two failed missions and one successful, so the fourth one decides between 2:2 and a win for the Resistance
  const { leader, view, spies, resistance } = await reachFourthMission(setup, ['fail', 'success', 'fail']);

  // One spy plays Fail among a team of four
  await submitTeam(leader, [spies[0], ...resistance.slice(0, view.requiredTeamSize! - 1)]);
  await voteAll(setup, 'Approve');
  await waitForPhase(setup, 'PlayingMission');
  await expect(
    setup.admin.getByRole('status').filter({ hasText: 'The mission fails from 2 Fail cards.' }),
  ).toBeVisible();
  await playMissionCards(setup, [spies[0], ...resistance.slice(0, view.requiredTeamSize! - 1)], [spies[0]]);

  const resolved = await waitForPhase(setup, 'MissionResult');
  expect(resolved.missionResults).toHaveLength(4);
  await expect(setup.admin.getByRole('status').filter({ hasText: 'The mission succeeded' })).toBeVisible();
  await continueMission(leader);

  // The score is 2:2 and the last mission needs only one Fail card
  const [, fifthLeader] = await waitForLeader(setup);
  expect((await viewOf(fifthLeader, session)).requiredFails).toBe(1);
  await playMission(setup, 'fail', 1);
  const over = await waitForPhase(setup, 'GameOver');
  expect(over.winner).toBe('Spy');
  expect(over.missionResults).toHaveLength(5);
});

test('two Fail cards fail the fourth mission', async ({ createSession, openAdmin, openGuest }) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest }, { players: 7 });
  const { leader, view, spies, resistance } = await reachFourthMission(setup, ['success', 'fail', 'success']);

  const team = [spies[0], spies[1], ...resistance.slice(0, view.requiredTeamSize! - 2)];
  await submitTeam(leader, team);
  await voteAll(setup, 'Approve');
  await waitForPhase(setup, 'PlayingMission');
  await playMissionCards(setup, team, [spies[0], spies[1]]);

  const resolved = await waitForPhase(setup, 'MissionResult');
  expect(resolved.missionResults).toHaveLength(4);
  await expect(setup.admin.getByRole('status').filter({ hasText: 'The mission failed (Fail cards: 2)' })).toBeVisible();
  // 2:2 again, so the game goes on
  expect(resolved.winner).toBeNull();
});
