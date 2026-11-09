import { Page, test as base, expect, request } from '@playwright/test';
import { ADMIN_STATE_FILE, API_URL } from './constants';
import { USER_PASSWORD, registerUser, uniqueUsername } from './fixtures/accounts';

// Not `test.use({ storageState })`: that would also make every API context made with `request.newContext` an admin's
const test = base.extend<{ page: Page }>({
  page: async ({ browser }, use) => {
    const context = await browser.newContext({ storageState: ADMIN_STATE_FILE });
    await use(await context.newPage());
    await context.close();
  },
});

test('an admin searches and pages through the registered users', async ({ page }) => {
  const prefix = uniqueUsername('pg');
  // Two pages of 20 (the page size is fixed); the prefix keeps the other tests' users out of the list
  for (let i = 0; i < 22; i++) {
    await (await registerUser(`${prefix}-${String(i).padStart(2, '0')}`)).dispose();
  }

  await page.goto('/admin/users');
  await page.getByRole('searchbox', { name: 'Search users by username' }).fill(prefix);

  await expect(page.getByText('Users: 22')).toBeVisible();
  await expect(page.getByText('Page 1 of 2')).toBeVisible();
  await expect(page.getByRole('listitem')).toHaveCount(20);
  await expect(page.getByText(`${prefix}-00`, { exact: true })).toBeVisible();

  await page.getByRole('button', { name: 'Next' }).click();
  await expect(page.getByText('Page 2 of 2')).toBeVisible();
  await expect(page.getByRole('listitem')).toHaveCount(2);
  await expect(page.getByText(`${prefix}-21`, { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Next' })).toBeDisabled();

  // A narrower search goes back to the first page
  await page.getByRole('searchbox', { name: 'Search users by username' }).fill(`${prefix}-07`);
  await expect(page.getByText('Users: 1')).toBeVisible();
  await expect(page.getByText('Page 1 of 1')).toBeVisible();
});

test('an admin resets a password: the user is signed out and only the new password works', async ({ page }) => {
  const username = uniqueUsername('reset');
  const newPassword = 'reset-by-admin-1';
  const user = await registerUser(username);

  await page.goto('/admin/users');
  await page.getByRole('searchbox', { name: 'Search users by username' }).fill(username);
  const row = page.getByRole('listitem').filter({ hasText: username });
  await row.getByRole('button', { name: 'Reset password' }).click();
  const dialog = page.getByRole('dialog', { name: `Reset the password of ${username}` });
  await dialog.getByLabel('New password').fill(newPassword);
  await dialog.getByRole('button', { name: 'Reset password' }).click();

  await expect(page.getByRole('status')).toContainText(`The password of ${username} was reset`);

  // The login the user had before the reset is dead
  expect((await user.get('/account/me')).status()).toBe(401);

  const fresh = await request.newContext({ baseURL: API_URL });
  expect((await fresh.post('/account/login', { data: { username, password: USER_PASSWORD } })).status()).toBe(401);
  expect((await fresh.post('/account/login', { data: { username, password: newPassword } })).status()).toBe(204);
  await user.dispose();
  await fresh.dispose();
});

test('a password under 8 characters cannot be set', async ({ page }) => {
  const username = uniqueUsername('short');
  await (await registerUser(username)).dispose();

  await page.goto('/admin/users');
  await page.getByRole('searchbox', { name: 'Search users by username' }).fill(username);
  await page.getByRole('listitem').filter({ hasText: username }).getByRole('button', { name: 'Reset password' }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel('New password').fill('short');

  await expect(dialog.getByRole('button', { name: 'Reset password' })).toBeDisabled();
});

test('the users are for admins only: a user and a visitor are refused', async ({ browser }) => {
  const username = uniqueUsername('nosy');
  const user = await registerUser(username);
  const visitor = await request.newContext({ baseURL: API_URL });

  expect((await user.get('/admin/users')).status()).toBe(403);
  expect((await user.put(`/admin/users/${crypto.randomUUID()}/password`, { data: { newPassword: 'whatever-123' } })).status()).toBe(403);
  expect((await visitor.get('/admin/users')).status()).toBe(401);

  // And the page itself shows a user nothing
  const context = await browser.newContext({ storageState: await user.storageState() });
  // The hint makes the page ask who is signed in, so it is the user's account that gets turned away
  await context.addInitScript(() => localStorage.setItem('partyking.accountHint', 'true'));
  const page = await context.newPage();
  await page.goto('/admin/users');
  await expect(page.getByText('Only an admin can see the users.')).toBeVisible();
  await context.close();
  await user.dispose();
  await visitor.dispose();
});

test('the admin password cannot be reset through the users', async () => {
  const admin = await request.newContext({ baseURL: API_URL, storageState: ADMIN_STATE_FILE });
  const me = await (await admin.get('/account/me')).json();

  expect(me.role).toBe('Admin');
  const response = await admin.put(`/admin/users/${me.id}/password`, { data: { newPassword: 'whatever-123' } });
  expect(response.status()).toBe(403);
  await admin.dispose();
});
