import { Page } from '@playwright/test';
import {
  playMission,
  rejectTeams,
  ResistanceGameSetup,
  spyNames,
  startResistanceGame,
  submitTeam,
  voteAll,
  waitForLeader,
  waitForPhase,
} from './fixtures/resistance';
import { expect, test } from './fixtures/test';

/** The admin's board lists the players; once the game is over every one of them carries the revealed role. */
async function expectRolesRevealed(admin: Page, setup: ResistanceGameSetup) {
  const spies = await spyNames(setup);
  for (const name of setup.guests.keys()) {
    const player = admin.locator('ul[aria-label="Players"]').first().locator(`li[title="${name}"]`);
    await expect(player).toContainText(spies.includes(name) ? 'Spy' : 'Resistance');
  }
}

test('three failed missions end the game for the spies and reveal every role', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest });

  for (let mission = 0; mission < 3; mission++) {
    await playMission(setup, 'fail');
  }

  const view = await waitForPhase(setup, 'GameOver');
  expect(view.winner).toBe('Spy');
  expect(view.missionResults).toHaveLength(3);

  const gameOver = 'Game over! The spies won: three missions failed (Resistance 0, Spies 3).';
  for (const page of [setup.admin, ...setup.guests.values()]) {
    await expect(page.getByRole('status').filter({ hasText: gameOver })).toBeVisible();
  }
  await expectRolesRevealed(setup.admin, setup);
});

test('five rejected teams in a row end the game for the spies', async ({ createSession, openAdmin, openGuest }) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest });

  await rejectTeams(setup, 5);

  const view = await waitForPhase(setup, 'GameOver');
  expect(view.winner).toBe('Spy');
  expect(view.rejectedTeams).toBe(5);
  for (const page of [setup.admin, ...setup.guests.values()]) {
    await expect(page.getByRole('status').filter({ hasText: 'The spies won: 5 teams in a row were rejected' })).toBeVisible();
  }
  await expectRolesRevealed(setup.admin, setup);
});

test('a member of the Resistance cannot play a Fail card, a spy can', async ({ createSession, openAdmin, openGuest }) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest });
  const spies = await spyNames(setup);
  const resistance = [...setup.guests.keys()].filter((name) => !spies.includes(name));

  // One spy and one member of the Resistance on the team
  const [, leader] = await waitForLeader(setup);
  const team = [spies[0], resistance[0]];
  await submitTeam(leader, team);
  await voteAll(setup, 'Approve');

  const fail = (name: string) =>
    setup.guests.get(name)!.getByRole('region', { name: 'Mission' }).getByRole('button', { name: 'Fail' });
  await expect(fail(resistance[0])).toBeDisabled();
  await expect(fail(spies[0])).toBeEnabled();

  // A disabled card is not just greyed out: the member of the Resistance can only play Success
  await setup.guests.get(resistance[0])!.getByRole('region', { name: 'Mission' }).getByRole('button', { name: 'Success' }).click();
  await fail(spies[0]).click();
  const view = await waitForPhase(setup, 'MissionResult');
  expect(view.missionResults).toHaveLength(1);
  await expect(setup.admin.getByRole('status').filter({ hasText: 'The mission failed (Fail cards: 1)' })).toBeVisible();
});
