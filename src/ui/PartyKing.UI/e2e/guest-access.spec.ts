import { expect, test } from './fixtures/test';

test('a guest gets no admin controls and is sent back from the games page', async ({ createSession, openGuest }) => {
  const session = await createSession();
  const guest = await openGuest(session);

  await expect(guest.getByRole('heading', { name: session.name })).toBeVisible();
  await expect(guest.getByRole('button', { name: 'Session settings' })).toBeHidden();
  await expect(guest.getByRole('button', { name: 'Show QR code' })).toBeHidden();

  // The server answers 403 and the page redirects to the session
  await guest.goto(`/s/${session.id}/games`);
  await expect(guest).toHaveURL(new RegExp(`/s/${session.id}$`));
  await expect(guest.getByRole('heading', { name: session.name })).toBeVisible();
  await expect(guest.getByRole('heading', { name: 'Games' })).toBeHidden();
});

test('an admin sees the settings and QR buttons and can open the games page', async ({ createSession, openAdmin }) => {
  const session = await createSession();
  const admin = await openAdmin(session);

  await expect(admin.getByRole('button', { name: 'Session settings' })).toBeVisible();
  await expect(admin.getByRole('button', { name: 'Show QR code' })).toBeVisible();

  await admin.goto(`/s/${session.id}/games`);
  await expect(admin.getByRole('heading', { name: 'Games' })).toBeVisible();
});
