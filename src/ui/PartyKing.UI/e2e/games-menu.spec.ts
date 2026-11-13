import { expect, test } from './fixtures/test';

test('the games icon is disabled for a guest until the host or an admin opens a game', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const guest = await openGuest(session);
  const admin = await openAdmin(session);

  const guestIcon = guest.getByRole('button', { name: 'Open games' });
  await expect(guestIcon).toBeDisabled();
  await expect(admin.getByRole('button', { name: 'Open games' })).toBeEnabled();

  // A click on a game without a lobby creates it and opens its board in a new tab
  await admin.getByRole('button', { name: 'Open games' }).click();
  const [board] = await Promise.all([
    admin.context().waitForEvent('page'),
    admin.getByRole('menuitem', { name: 'Ringo' }).click(),
  ]);
  await expect(board.getByRole('button', { name: 'Open the game' })).toBeVisible();

  // A lobby that is not open yet is no reason for a guest to join
  await expect(guestIcon).toBeDisabled();
  await admin.getByRole('button', { name: 'Open games' }).click();
  await expect(admin.getByRole('menuitem', { name: 'Ringo Lobby' })).toBeVisible();
  await admin.keyboard.press('Escape');

  await board.getByRole('button', { name: 'Open the game' }).click();

  // The push enables the guest's icon without a reload; only the open game can be chosen
  await expect(guestIcon).toBeEnabled();
  await guestIcon.click();
  await expect(guest.getByRole('menuitem', { name: 'Ringo' })).toBeEnabled();
  await expect(guest.getByRole('menuitem', { name: 'Resistance' })).toBeDisabled();
});
