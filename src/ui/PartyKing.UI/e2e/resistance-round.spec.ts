import {
  playMission,
  rejectTeams,
  startResistanceGame,
  submitTeam,
  viewOf,
  vote,
  waitForLeader,
  waitForPhase,
} from './fixtures/resistance';
import { expect, test } from './fixtures/test';

test('a full Resistance game: three successful missions end it for the Resistance', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest });

  // Five players: 2, 3 and 2 people go on the first three missions
  const teamSizes: (number | null)[] = [];
  for (let mission = 0; mission < 3; mission++) {
    const [, leader] = await waitForLeader(setup);
    teamSizes.push((await viewOf(leader, session)).requiredTeamSize);
    await playMission(setup, 'success');
  }
  expect(teamSizes).toEqual([2, 3, 2]);

  const gameOver = 'Game over! The Resistance won: three missions succeeded (Resistance 3, Spies 0).';
  for (const page of [setup.admin, ...setup.guests.values()]) {
    await expect(page.getByRole('status').filter({ hasText: gameOver })).toBeVisible();
  }
});

test('a rejected team passes the lead on and counts towards the five-rejection loss', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest });

  const [firstName] = await waitForLeader(setup);
  const mission = (await waitForPhase(setup, 'SelectingTeam')).missionNumber;
  await rejectTeams(setup, 1);

  // The lead moved to another player, who picks a new team for the same mission
  const [secondName, second] = await waitForLeader(setup);
  expect(secondName).not.toBe(firstName);
  await expect(second.getByRole('status').filter({ hasText: 'Rejected teams in a row: 1/5' })).toBeVisible();
  expect((await waitForPhase(setup, 'SelectingTeam')).missionNumber).toBe(mission);
});

test('only the leader gets the team picker, and only a team of the right size can be submitted', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest });
  const [leaderName, leader] = await waitForLeader(setup);

  for (const [name, page] of setup.guests) {
    await expect(page.getByRole('group', { name: 'Pick the team' })).toBeVisible({ visible: name === leaderName });
  }

  // The first mission of a five-player game takes two players
  const picker = leader.getByRole('group', { name: 'Pick the team' });
  const submit = leader.getByRole('button', { name: 'Submit the team for a vote' });
  const [first, second, third] = [...setup.guests.keys()].filter((name) => name !== leaderName);
  await expect(submit).toBeDisabled();
  await picker.getByText(first, { exact: true }).click();
  await expect(submit).toBeDisabled();
  await picker.getByText(second, { exact: true }).click();
  await expect(submit).toBeEnabled();

  // The team is full: nobody else can be picked, and unpicking gives the room back
  await expect(picker.getByRole('checkbox', { name: third, exact: true })).toBeDisabled();
  await picker.getByText(second, { exact: true }).click();
  await expect(submit).toBeDisabled();
  await expect(picker.getByRole('checkbox', { name: third, exact: true })).toBeEnabled();
});

test('a tied vote rejects the team', async ({ createSession, openAdmin, openGuest }) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest }, { players: 6 });
  const [leaderName, leader] = await waitForLeader(setup);
  const names = [...setup.guests.keys()];
  const size = (await viewOf(leader, session)).requiredTeamSize!;
  await submitTeam(leader, names.filter((name) => name !== leaderName).slice(0, size));

  // Three for and three against
  for (const [index, page] of [...setup.guests.values()].entries()) {
    await vote(page, index < 3 ? 'Approve' : 'Reject');
  }

  await expect(setup.admin.getByRole('status').filter({ hasText: 'Rejected teams in a row: 1/5' })).toBeVisible();
  const view = await waitForPhase(setup, 'SelectingTeam');
  expect(view.rejectedTeams).toBe(1);
});
