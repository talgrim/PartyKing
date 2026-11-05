import {
  PLAYER_NAMES,
  spyNames,
  startResistanceGame,
  submitTeam,
  viewOfPlayer,
  voteAll,
  waitForLeader,
  waitForPhase,
} from './fixtures/resistance';
import { expect, test } from './fixtures/test';

test('the Reverser module deals one Reverser to each side and a Reverse reverses the mission', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest }, { reverser: true });
  const names = PLAYER_NAMES.slice(0, setup.guests.size);
  const spies = await spyNames(setup);

  // Everybody knows only their own character
  const reversers: string[] = [];
  for (const name of names) {
    const view = await viewOfPlayer(setup, name);
    expect(['Standard', 'Reverser']).toContain(view.yourCharacter);
    if (view.yourCharacter === 'Reverser') {
      reversers.push(name);
    }

    const page = setup.guests.get(name)!;
    await page.getByRole('button', { name: 'Show your role' }).click();
    const note = page.getByText(/You are a Reverser/);
    await (view.yourCharacter === 'Reverser' ? expect(note).toBeVisible() : expect(note).toBeHidden());
  }
  expect(reversers).toHaveLength(2);
  expect(reversers.filter((name) => spies.includes(name))).toHaveLength(1);

  // The Resistance's Reverser and a player who is not a Reverser go on the first mission: Reverse turns Success around
  const reverser = reversers.find((name) => !spies.includes(name))!;
  const partner = names.find((name) => !reversers.includes(name))!;
  const [, leader] = await waitForLeader(setup);
  await submitTeam(leader, [reverser, partner]);
  await voteAll(setup, 'Approve');

  const mine = setup.guests.get(reverser)!.getByRole('region', { name: 'Mission' });
  await expect(mine.getByRole('button', { name: 'Reverse' })).toBeEnabled();
  await expect(mine.getByRole('button', { name: 'Fail' })).toHaveAttribute('aria-disabled', 'true');
  const theirs = setup.guests.get(partner)!.getByRole('region', { name: 'Mission' });
  // Everybody on the team has the card, so having one tells nothing, but only a Reverser may play it
  await expect(theirs.getByRole('button', { name: 'Reverse' })).toHaveAttribute('aria-disabled', 'true');

  await mine.getByRole('button', { name: 'Reverse' }).click();
  await theirs.getByRole('button', { name: 'Success' }).click();

  const view = await waitForPhase(setup, 'MissionResult');
  expect(view.missionResults).toHaveLength(1);
  expect(view.missionResults[0].succeeded).toBe(false);
  // The played cards are public once the mission is over (not who played them)
  expect(view.missionResults[0]).toMatchObject({ failCount: 0, reverseCount: 1 });
  const status = setup.admin.getByRole('status').filter({ hasText: 'The mission failed (Fail cards: 0) (Reverse cards: 1)' });
  await expect(status).toBeVisible();
});
