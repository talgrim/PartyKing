import { expect, test } from '@playwright/test';
import {
  USER_PASSWORD,
  logInThroughTheDialog,
  logOutFromTheMenu,
  openAccountSettings,
  signUpThroughTheDialog,
  uniqueUsername,
} from './fixtures/accounts';

test('a visitor signs up, stays signed in after a reload, logs out and logs back in', async ({ page }) => {
  const username = uniqueUsername();

  await signUpThroughTheDialog(page, username);
  await page.reload();
  await expect(page.getByRole('button', { name: username })).toBeVisible();

  await openAccountSettings(page, username);
  await expect(page.getByText(`Signed in as ${username}`)).toBeVisible();
  await page.getByRole('dialog').getByRole('button', { name: 'Close' }).click();
  await expect(page.getByRole('dialog')).toBeHidden();

  await logOutFromTheMenu(page, username);

  await logInThroughTheDialog(page, username);
});

test('a username that is taken is refused, whatever its case', async ({ page }) => {
  const username = uniqueUsername('taken');
  await signUpThroughTheDialog(page, username);

  await page.context().clearCookies();
  await page.goto('/');
  await page.evaluate(() => localStorage.clear());
  await page.reload();
  await page.getByRole('button', { name: 'Log in' }).click();
  await page.getByRole('tab', { name: 'Sign up' }).click();
  await page.getByLabel('Username').fill(username.toUpperCase());
  await page.getByLabel('Password').fill(USER_PASSWORD);
  await page.getByRole('button', { name: 'Sign up' }).click();

  await expect(page.getByRole('alert')).toHaveText('The username is already taken');
});

test('a wrong password is refused', async ({ page }) => {
  const username = uniqueUsername('wrong');
  await signUpThroughTheDialog(page, username);
  await logOutFromTheMenu(page, username);

  await page.getByRole('button', { name: 'Log in' }).click();
  await page.getByLabel('Username').fill(username);
  await page.getByLabel('Password').fill('not-the-password');
  await page.getByRole('button', { name: 'Log in' }).click();

  await expect(page.getByRole('alert')).toHaveText('Invalid username or password');
});

test('a user changes the password: the old one stops working and the new one works', async ({ page }) => {
  const username = uniqueUsername('change');
  const newPassword = 'brand-new-password-1';
  await signUpThroughTheDialog(page, username);

  await openAccountSettings(page, username);
  await page.getByLabel('Current password').fill(USER_PASSWORD);
  await page.getByLabel('New password').fill(newPassword);
  await page.getByRole('button', { name: 'Change password' }).click();
  await expect(page.getByRole('alert')).toHaveText('Password changed');

  // This browser stays signed in, the login with the old password does not work
  await page.reload();
  await expect(page.getByRole('button', { name: username })).toBeVisible();
  await logOutFromTheMenu(page, username);

  await page.getByRole('button', { name: 'Log in' }).click();
  await page.getByLabel('Username').fill(username);
  await page.getByLabel('Password').fill(USER_PASSWORD);
  await page.getByRole('button', { name: 'Log in' }).click();
  await expect(page.getByRole('alert')).toHaveText('Invalid username or password');

  await page.getByLabel('Password').fill(newPassword);
  await page.getByRole('button', { name: 'Log in' }).click();
  await expect(page.getByRole('button', { name: username })).toBeVisible();
});

test('a wrong current password does not change anything', async ({ page }) => {
  const username = uniqueUsername('keep');
  await signUpThroughTheDialog(page, username);

  await openAccountSettings(page, username);
  await page.getByLabel('Current password').fill('not-the-password');
  await page.getByLabel('New password').fill('brand-new-password-1');
  await page.getByRole('button', { name: 'Change password' }).click();

  await expect(page.getByRole('alert')).toHaveText('The current password is incorrect');
});

test('logging out everywhere ends the login of the other browsers too', async ({ browser }) => {
  const username = uniqueUsername('everywhere');
  const first = await browser.newContext();
  const second = await browser.newContext();
  const firstPage = await first.newPage();
  const secondPage = await second.newPage();
  await signUpThroughTheDialog(firstPage, username);
  await logInThroughTheDialog(secondPage, username);

  firstPage.once('dialog', (dialog) => dialog.accept());
  await openAccountSettings(firstPage, username);
  await firstPage.getByRole('button', { name: 'Log out everywhere' }).click();
  await expect(firstPage.getByRole('button', { name: 'Log in' })).toBeVisible();

  await secondPage.reload();
  await expect(secondPage.getByRole('button', { name: 'Log in' })).toBeVisible();
  await first.close();
  await second.close();
});

test('a user deletes the account, which can then no longer log in', async ({ page }) => {
  const username = uniqueUsername('delete');
  await signUpThroughTheDialog(page, username);

  await openAccountSettings(page, username);
  await page.getByRole('button', { name: 'Delete account' }).click();
  await page.getByLabel('Password to confirm').fill(USER_PASSWORD);
  await page.getByRole('button', { name: 'Delete my account' }).click();
  await expect(page.getByRole('button', { name: 'Log in' })).toBeVisible();

  await page.getByRole('button', { name: 'Log in' }).click();
  await page.getByLabel('Username').fill(username);
  await page.getByLabel('Password').fill(USER_PASSWORD);
  await page.getByRole('button', { name: 'Log in' }).click();
  await expect(page.getByRole('alert')).toHaveText('Invalid username or password');
});

test('a user is not an admin: the admin panel turns them away', async ({ page }) => {
  const username = uniqueUsername('plain');
  await signUpThroughTheDialog(page, username);
  await expect(page.getByRole('link', { name: 'Admin panel' })).toBeHidden();

  await page.goto('/admin');

  await expect(page.getByText(`You are signed in as ${username}, which is not an admin account.`)).toBeVisible();
  await expect(page.getByLabel('New session name')).toBeHidden();
});

test('the account button is on every page, not only the landing page', async ({ page }) => {
  const username = uniqueUsername('everypage');
  await signUpThroughTheDialog(page, username);

  for (const path of ['/admin', '/admin/users', '/s/00000000-0000-0000-0000-000000000000']) {
    await page.goto(path);
    await expect(page.getByRole('button', { name: username })).toBeVisible();
  }

  // The header also leads back to the list of sessions, except on the landing page itself
  await page.goto('/admin');
  await page.getByRole('link', { name: 'All sessions' }).click();
  await expect(page).toHaveURL('/');
  await expect(page.getByRole('link', { name: 'All sessions' })).toBeHidden();

  // ...and what it opens works there too
  await page.goto('/admin');
  await openAccountSettings(page, username);
  await expect(page.getByText(`Signed in as ${username}`)).toBeVisible();
});
