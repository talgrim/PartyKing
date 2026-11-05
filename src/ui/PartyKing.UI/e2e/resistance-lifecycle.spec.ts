import { rejectTeams, startResistanceGame, viewOf, waitForPhase } from './fixtures/resistance';
import { expect, test } from './fixtures/test';

test('a finished game can be restarted with the same players and a fresh deal', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest });
  const { admin, guests } = setup;

  // Only a finished game offers a restart
  await expect(admin.getByRole('button', { name: 'Restart game' })).toBeHidden();
  await rejectTeams(setup, 5);
  await waitForPhase(setup, 'GameOver');
  await admin.getByRole('button', { name: 'Restart game' }).click();

  // Back in the lobby: nobody was removed and the admin deals again
  await expect(admin.getByText('5/10 players')).toBeVisible();
  await expect(admin.getByRole('button', { name: 'Restart game' })).toBeHidden();
  const lobby = await viewOf([...guests.values()][0], session);
  expect(lobby.startedAt).toBeNull();
  expect(lobby.players).toHaveLength(5);

  await admin.getByRole('button', { name: 'Start game' }).click();
  const view = await waitForPhase(setup, 'SelectingTeam');
  expect(view.winner).toBeNull();
  expect(view.rejectedTeams).toBe(0);
  expect(view.missionResults).toHaveLength(0);
  for (const guest of guests.values()) {
    await expect(guest.getByRole('button', { name: 'Show your role' })).toBeVisible();
  }
});

test('ending the game asks first and then sends everybody away', async ({ createSession, openAdmin, openGuest }) => {
  const session = await createSession();
  const { admin, guests } = await startResistanceGame(session, { openAdmin, openGuest });

  await admin.getByRole('button', { name: 'End game' }).click();
  const dialog = admin.getByRole('dialog', { name: 'End the game?' });
  await expect(dialog).toBeVisible();

  // Cancelling keeps the game
  await dialog.getByRole('button', { name: 'Cancel' }).click();
  await expect(dialog).toBeHidden();
  await expect(admin.getByRole('status')).toBeVisible();

  await admin.getByRole('button', { name: 'End game' }).click();
  await dialog.getByRole('button', { name: 'End game' }).click();

  await expect(admin).toHaveURL(`/s/${session.id}/games`);
  for (const guest of guests.values()) {
    await expect(guest).toHaveURL(new RegExp(`/s/${session.id}$`));
  }
  await admin.getByRole('button', { name: 'Create' }).click();
  await expect(admin.getByText('0/10 players')).toBeVisible();
});
