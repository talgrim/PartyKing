import { Page } from '@playwright/test';
import { findPlayer, isLeader, isSpy, startResistanceGame } from './fixtures/resistance';
import { expect, test } from './fixtures/test';

const TEAM_SIZES = [2, 3, 2]; // missions 1 to 3 with five players

/** The leader picks the first `size` other players and submits the team. */
async function submitTeam(leader: Page, names: string[], size: number): Promise<string[]> {
  const picker = leader.getByRole('group', { name: 'Pick the team' });
  for (const name of names.slice(0, size)) {
    // The checkbox itself is visually hidden under a tick; the label text is what a person taps
    await picker.getByText(name, { exact: true }).click();
    await expect(picker.getByRole('checkbox', { name, exact: true })).toBeChecked();
  }
  await leader.getByRole('button', { name: 'Submit the team for a vote' }).click();
  return names.slice(0, size);
}

const vote = (page: Page, choice: 'Approve' | 'Reject') =>
  page.getByRole('region', { name: 'Voting' }).getByRole('button', { name: choice }).click();

test('a full Resistance game: three successful missions end it for the Resistance', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const { admin, guests } = await startResistanceGame(session, { openAdmin, openGuest });

  // Everyone can look at their role; the spies are told who their fellow spies are
  const roles = await Promise.all([...guests.values()].map((page) => isSpy(page)));
  expect(roles.filter(Boolean)).toHaveLength(2);

  for (const [index, size] of TEAM_SIZES.entries()) {
    const [leaderName, leader] = await findPlayer(guests, isLeader);
    const others = [...guests.keys()].filter((name) => name !== leaderName);
    const team = await submitTeam(leader, others, size);

    for (const page of guests.values()) {
      await vote(page, 'Approve');
    }

    // Only the team plays a mission card (all play Success: the Resistance has to)
    for (const name of team) {
      const mission = guests.get(name)!.getByRole('region', { name: 'Mission' });
      // The panel goes away with the last card (it resolves the mission), so only the clicks are made here
      await mission.getByRole('button', { name: 'Success' }).click();
    }

    if (index < TEAM_SIZES.length - 1) {
      // The mission is over; the leader starts the next one
      const next = leader.getByRole('button', { name: 'Next', exact: true });
      await expect(next).toBeVisible();
      await next.click();
      await expect(next).toBeHidden();
    }
  }

  const gameOver = 'Game over! The Resistance won: three missions succeeded (Resistance 3, Spies 0).';
  for (const page of [admin, ...guests.values()]) {
    await expect(page.getByRole('status').filter({ hasText: gameOver })).toBeVisible();
  }
});

test('a rejected team passes the lead on and counts towards the five-rejection loss', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const { guests } = await startResistanceGame(session, { openAdmin, openGuest });

  const [firstName, first] = await findPlayer(guests, isLeader);
  await submitTeam(first, [...guests.keys()].filter((name) => name !== firstName), 2);
  for (const page of guests.values()) {
    await vote(page, 'Reject');
  }

  // The lead moved to another player, who picks a new team for the same mission
  const [secondName, second] = await findPlayer(guests, isLeader);
  expect(secondName).not.toBe(firstName);
  await expect(second.getByRole('status').filter({ hasText: 'Rejected teams in a row: 1/5' })).toBeVisible();
});
