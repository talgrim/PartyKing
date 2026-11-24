import { Page } from '@playwright/test';
import {
  continueMission,
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

  // The first mission is played by hand: a member of the Resistance cannot play a Fail card, a spy can
  const spies = await spyNames(setup);
  const resistance = [...setup.guests.keys()].filter((name) => !spies.includes(name));
  const [, leader] = await waitForLeader(setup);
  await submitTeam(leader, [spies[0], resistance[0]]);
  await voteAll(setup, 'Approve');

  const card = (name: string, kind: 'Fail' | 'Success') =>
    setup.guests.get(name)!.getByRole('region', { name: 'Mission' }).getByRole('button', { name: kind });
  await expect(card(resistance[0], 'Fail')).toBeDisabled();
  await expect(card(spies[0], 'Fail')).toBeEnabled();
  // A disabled card is not just greyed out: the member of the Resistance can only play Success
  await card(resistance[0], 'Success').click();
  await card(spies[0], 'Fail').click();
  const first = await waitForPhase(setup, 'MissionResult');
  expect(first.missionResults).toHaveLength(1);
  await expect(setup.admin.getByRole('status').filter({ hasText: 'The mission failed (Fail cards: 1)' })).toBeVisible();
  await continueMission(leader);

  for (let mission = 1; mission < 3; mission++) {
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
