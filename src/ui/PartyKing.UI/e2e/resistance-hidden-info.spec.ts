import { expect, test } from './fixtures/test';
import {
  PLAYER_NAMES,
  spyNames,
  startResistanceGame,
  submitTeam,
  viewOf,
  viewOfPlayer,
  vote,
  waitForLeader,
} from './fixtures/resistance';

test('only spies are told who the spies are', async ({ createSession, openAdmin, openGuest }) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest });
  const spies = await spyNames(setup);
  expect(spies).toHaveLength(2);

  for (const [name, page] of setup.guests) {
    await page.getByRole('button', { name: 'Show your role' }).click();
    await expect(page.getByRole('button', { name: 'Hide your role' })).toBeVisible();
    const fellows = page.getByRole('heading', { name: 'Your fellow spies' });
    const view = await viewOfPlayer(setup, name);

    if (spies.includes(name)) {
      const other = spies.find((spy) => spy !== name)!;
      await expect(fellows).toBeVisible();
      // The fellow spy is named on the card, and only that one
      await expect(page.getByRole('list', { name: 'Fellow spies' })).toHaveText(other);
      expect(view.fellowSpies).toHaveLength(1);
    } else {
      await expect(fellows).toBeHidden();
      expect(view.yourRole).toBe('Resistance');
      expect(view.fellowSpies).toBeNull();
    }
  }
});

test('the admin and a guest who only watches get no role', async ({ createSession, openAdmin, openGuest }) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest });
  const watcher = await openGuest(session);
  await watcher.goto(`/s/${session.id}/games/resistance`);

  for (const page of [setup.admin, watcher]) {
    const view = await viewOf(page, session);
    expect(view.yourPlayerId).toBeNull();
    expect(view.yourRole).toBeNull();
    expect(view.fellowSpies).toBeNull();
    expect(view.players).toHaveLength(5);
  }
  await expect(watcher.getByRole('button', { name: 'Show your role' })).toBeHidden();
});

test('an admin who plays sees their role, but not on the TV view', async ({ createSession, openAdmin, openGuest }) => {
  const session = await createSession();
  const admin = await openAdmin(session);
  await admin.goto(`/s/${session.id}/games`);
  await admin.getByRole('button', { name: 'Create' }).click();
  await admin.getByRole('button', { name: 'Open the game' }).click();

  // The admin takes a seat (no name: they join as "admin") next to four guests
  await admin.getByRole('link', { name: 'Join as a player' }).click();
  await admin.getByRole('button', { name: 'Join' }).click();
  for (const name of PLAYER_NAMES.slice(0, 4)) {
    const guest = await openGuest(session);
    await guest.goto(`/s/${session.id}/games/resistance/join`);
    await guest.getByLabel('Your name').fill(name);
    await guest.getByRole('button', { name: 'Join' }).click();
    await expect(guest.getByRole('button', { name: 'Show your role' })).toBeHidden();
  }
  await admin.goto(`/s/${session.id}/games/resistance`);
  await expect(admin.getByText('5/10 players')).toBeVisible();
  await admin.getByRole('button', { name: 'Controls' }).click();
  await admin.getByRole('button', { name: 'Start game' }).click();

  await expect(admin.getByRole('button', { name: 'Show your role' })).toBeVisible();
  expect((await viewOf(admin, session)).yourRole).not.toBeNull();

  // The TV tab shares the cookie of the admin who plays, so it must blank what is private to them
  const tv = await admin.context().newPage();
  await tv.goto(`/s/${session.id}/games/resistance?tv=1`);
  await expect(tv.getByRole('status')).toBeVisible();
  await expect(tv.getByRole('button', { name: 'Show your role' })).toBeHidden();
  await expect(tv.getByRole('button', { name: 'Hide your role' })).toBeHidden();
});

test('votes stay secret until everybody has voted, and can be changed until then', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest });
  const [leaderName, leader] = await waitForLeader(setup);
  const names = [...setup.guests.keys()];
  const others = names.filter((name) => name !== leaderName);
  await submitTeam(leader, others.slice(0, 2));

  const [first, ...rest] = names;
  const last = rest.pop()!;

  // The first voter changes their mind
  await vote(setup.guests.get(first)!, 'Approve');
  await expect(
    setup.guests.get(first)!.getByText('Your vote is saved. You can change it until everyone has voted.'),
  ).toBeVisible();
  await vote(setup.guests.get(first)!, 'Reject');
  await expect.poll(async () => (await viewOfPlayer(setup, first)).yourVote).toBe(false);

  for (const name of rest) {
    await vote(setup.guests.get(name)!, 'Approve');
  }

  // Four of five have voted: everybody can see who voted, but nobody how
  await expect(setup.admin.getByRole('status').filter({ hasText: 'Voted: 4/5.' })).toBeVisible();
  for (const name of names) {
    const view = await viewOfPlayer(setup, name);
    expect(view.votedPlayerIds).toHaveLength(4);
    expect(view.votes).toBeNull();
  }
  expect((await viewOf(setup.admin, session)).votes).toBeNull();
  expect((await viewOfPlayer(setup, last)).yourVote).toBeNull();

  // The last vote opens them all
  await vote(setup.guests.get(last)!, 'Approve');
  await expect.poll(async () => (await viewOf(setup.admin, session)).votes).toHaveLength(5);
  const votes = (await viewOf(setup.admin, session)).votes!;
  expect(votes.filter((x) => !x.approve)).toHaveLength(1);
  await expect(setup.admin.getByRole('status').filter({ hasText: 'for: 4, against: 1' })).toBeVisible();
});
