import { expect, test } from './fixtures/test';

test('the host or an admin renames the session and the new name shows everywhere', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const admin = await openAdmin(session);
  const newName = `${session.name} renamed`;

  await admin.getByRole('button', { name: 'Session settings' }).click();
  await admin.getByLabel('Session name', { exact: true }).fill(newName);
  await admin.getByRole('button', { name: 'Save name' }).click();
  await expect(admin.getByText('Name saved')).toBeVisible();
  await admin.getByRole('button', { name: 'Close' }).click();
  await expect(admin.getByRole('heading', { name: newName })).toBeVisible();

  // The landing page, seen by a guest, lists it under the new name
  const guest = await openGuest(session);
  await guest.goto('/');
  await expect(guest.getByText(newName, { exact: true })).toBeVisible();
});

test('changing the password locks out guests who know the old one, but not the one who changed it', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const admin = await openAdmin(session);
  const guest = await openGuest(session);
  const newPassword = `${session.password}-new`;

  await admin.getByRole('button', { name: 'Session settings' }).click();
  await admin.getByLabel('New password').fill(newPassword);
  await admin.getByRole('button', { name: 'Change password' }).click();
  await expect(admin.getByText('Password changed. Guests have to enter the new one.')).toBeVisible();

  // The admin stays unlocked
  await admin.reload();
  await expect(admin.getByRole('heading', { name: session.name })).toBeVisible();

  // The guest's unlock cookie stopped working: the old password is refused, the new one lets them in
  await guest.reload();
  await expect(guest.getByRole('heading', { name: 'This session is password protected' })).toBeVisible();
  await guest.getByLabel('Session password').fill(session.password);
  await guest.getByRole('button', { name: 'Join' }).click();
  await expect(guest.getByText('Wrong password')).toBeVisible();

  await guest.getByLabel('Session password').fill(newPassword);
  await guest.getByRole('button', { name: 'Join' }).click();
  await expect(guest.getByRole('heading', { name: session.name })).toBeVisible();
});
