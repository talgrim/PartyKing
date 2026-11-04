import { ADMIN_PASSWORD, ADMIN_USERNAME } from './constants';
import { expect, test } from './fixtures/test';

test('an admin logs in, creates a session and guests find it by its code', async ({ page, browser }) => {
  const name = `Admin made ${Date.now().toString(36)}`;

  await page.goto('/admin');
  await page.getByLabel('Username').fill(ADMIN_USERNAME);
  await page.getByLabel('Password').fill(ADMIN_PASSWORD);
  await page.getByRole('button', { name: 'Log in' }).click();

  await page.getByLabel('New session name').fill(name);
  await page.getByLabel('New session password').fill('party-pass');
  await page.getByRole('button', { name: 'Create session' }).click();

  await expect(page.getByText('Session created')).toBeVisible();
  const row = page.getByRole('listitem').filter({ hasText: name });
  await expect(row).toBeVisible();
  const code = (await row.locator('.font-mono').innerText()).trim();
  expect(code).not.toBe('');

  // A guest (no cookies) finds it on the landing page by the code
  const guest = await browser.newContext();
  const guestPage = await guest.newPage();
  await guestPage.goto('/');
  await guestPage.getByLabel('Search sessions by code').fill(code);
  await expect(guestPage.getByRole('link', { name: new RegExp(name) })).toBeVisible();
  await guestPage.getByLabel('Search sessions by code').fill('no-such-code');
  await expect(guestPage.getByText('No session matches “no-such-code”.')).toBeVisible();
  await guest.close();

  page.once('dialog', (dialog) => dialog.accept());
  await row.getByRole('button', { name: 'Delete' }).click();
  await expect(page.getByText('Session deleted')).toBeVisible();
});

test('a wrong admin password is refused', async ({ page }) => {
  await page.goto('/admin');
  await page.getByLabel('Username').fill(ADMIN_USERNAME);
  await page.getByLabel('Password').fill('not-the-password');
  await page.getByRole('button', { name: 'Log in' }).click();

  await expect(page.getByLabel('New session name')).toBeHidden();
  await expect(page.getByRole('button', { name: 'Log in' })).toBeVisible();
});
