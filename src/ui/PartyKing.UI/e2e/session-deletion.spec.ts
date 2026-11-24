import { API_URL } from './constants';
import { expect, test } from './fixtures/test';

// start-api.mjs makes a requested deletion happen after 15 s; the tests that cancel one must be quicker than that
test('an admin deletes a session from its settings at once, and it is gone from the list', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const admin = await openAdmin(session);
  const guest = await openGuest(session);

  await admin.getByRole('button', { name: 'Session settings' }).click();
  admin.once('dialog', (dialog) => dialog.accept());
  await admin.getByRole('button', { name: 'Delete session' }).click();

  await expect(admin).toHaveURL('/');
  const listed = await guest.request.get(`${API_URL}/sessions`);
  expect((await listed.json()).map((item: { id: string }) => item.id)).not.toContain(session.id);
});

test('a guest asks for the deletion and sees the date, the session is deleted when it comes, and the guest is told', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const guest = await openGuest(session);
  const admin = await openAdmin(session);
  await expect(admin.getByRole('button', { name: 'Request deletion of this session' })).toBeHidden();

  guest.once('dialog', (dialog) => dialog.accept());
  await guest.getByRole('button', { name: 'Request deletion of this session' }).click();

  const banner = guest.getByRole('status').filter({ hasText: 'Scheduled for deletion' });
  await expect(banner).toBeVisible();
  await expect(guest.getByRole('button', { name: 'Request deletion of this session' })).toBeHidden();
  // Nobody but an admin can call it off, so the banner has no button
  await expect(banner.getByRole('button')).toHaveCount(0);

  // The worker deletes it once the time has come. The open page learns it on its next request (it polls every
  // 30 s and asks again when the tab gets the focus back, which is what this does)
  await expect(async () => {
    await guest.evaluate(() => window.dispatchEvent(new Event('focus')));
    await expect(guest.getByRole('heading', { name: 'This session was deleted' })).toBeVisible({ timeout: 2_000 });
  }).toPass({ timeout: 40_000, intervals: [1_000] });
});

test('only an admin can call a requested deletion off, from the admin panel', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const guest = await openGuest(session);
  const page = await openAdmin(session);
  guest.once('dialog', (dialog) => dialog.accept());
  await guest.getByRole('button', { name: 'Request deletion of this session' }).click();
  await expect(guest.getByRole('status').filter({ hasText: 'Scheduled for deletion' })).toBeVisible();

  const refused = await guest.request.delete(`${API_URL}/admin/sessions/${session.id}/deletion-request`);
  expect(refused.status()).toBe(401);

  await page.goto('/admin');
  const row = page.getByRole('listitem').filter({ hasText: session.name });
  await expect(row.getByText(/Deletion requested by a guest/)).toBeVisible();
  page.once('dialog', (dialog) => dialog.accept());
  await row.getByRole('button', { name: 'Cancel deletion' }).click();

  await expect(page.getByText('Deletion cancelled')).toBeVisible();
  await expect(row.getByText(/Deletion requested by a guest/)).toBeHidden();
  const after = await guest.request.get(`${API_URL}/sessions/${session.id}`);
  expect((await after.json()).deletionScheduledAt ?? null).toBeNull();
});
