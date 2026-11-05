import { expect, test } from './fixtures/test';

test('a guest who opens the QR link gets in without the password', async ({ browser, createSession, openAdmin }) => {
  const session = await createSession();
  const admin = await openAdmin(session);

  // The dialog shows the link only as a QR image, so read the token from the response that creates it
  const created = admin.waitForResponse((response) => response.url().endsWith(`/sessions/${session.id}/join-token`));
  await admin.getByRole('button', { name: 'Show QR code' }).click();
  await expect(admin.getByRole('dialog', { name: 'Scan to join without the password' })).toBeVisible();
  const { token } = await (await created).json();
  expect(token).toBeTruthy();

  const context = await browser.newContext();
  const guest = await context.newPage();
  await guest.goto(`/s/${session.id}?join=${encodeURIComponent(token)}`);
  await expect(guest.getByRole('heading', { name: session.name })).toBeVisible();
  await expect(guest.getByLabel('Session password')).toBeHidden();
  // The token is stripped from the address bar, and the unlock is a cookie, so it survives a reload
  await expect(guest).not.toHaveURL(/join=/);
  await guest.reload();
  await expect(guest.getByRole('heading', { name: session.name })).toBeVisible();
  await context.close();
});

test('a bad join token falls back to the password form', async ({ page, createSession }) => {
  const session = await createSession();

  await page.goto(`/s/${session.id}?join=not-a-real-token`);
  await expect(page.getByRole('heading', { name: 'This session is password protected' })).toBeVisible();
});
