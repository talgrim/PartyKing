import { ADMIN_PASSWORD, ADMIN_USERNAME } from './constants';
import { expect, test } from './fixtures/test';

test('an admin logs in, creates a session and guests find it by its code', async ({ page, browser }) => {
  const name = `Admin made ${Date.now().toString(36)}`;

  await page.goto('/admin');
  await page.getByRole('link', { name: 'Log in' }).first().click();
  await page.getByLabel('Username').fill(ADMIN_USERNAME);
  await page.getByLabel('Password').fill(ADMIN_PASSWORD);
  await page.getByRole('button', { name: 'Log in' }).click();

  // The identity service sent the browser back to the admin page
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

test('a wrong admin password is refused by the identity service, and the admin page stays shut', async ({ page }) => {
  await page.goto('/admin');
  await page.getByRole('link', { name: 'Log in' }).first().click();
  await page.getByLabel('Username').fill(ADMIN_USERNAME);
  await page.getByLabel('Password').fill('not-the-password');
  await page.getByRole('button', { name: 'Log in' }).click();

  await expect(page.getByRole('alert')).toHaveText('Invalid username or password');
  await page.goto('/admin');
  await expect(page.getByLabel('New session name')).toBeHidden();
  await expect(page.getByRole('link', { name: 'Log in' }).first()).toBeVisible();
});
