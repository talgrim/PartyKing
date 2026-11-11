import { APIRequestContext, Page, expect, request } from '@playwright/test';
import { API_URL } from '../constants';

export const USER_PASSWORD = 'user-password-1';

let counter = 0;

/** A username no other test uses; the database is shared by the whole run. */
export const uniqueUsername = (prefix = 'user') => `${prefix}-${Date.now().toString(36)}-${counter++}`;

/** Registers a user through the API; the returned context carries the new login cookie. */
export async function registerUser(username: string, password = USER_PASSWORD): Promise<APIRequestContext> {
  const api = await request.newContext({ baseURL: API_URL });
  const response = await api.post('/account/register', { data: { username, password } });
  expect(response.status()).toBe(204);

  return api;
}

/** Opens the account dialog of the landing page and signs up through it; resolves once the dialog is gone. */
export async function signUpThroughTheDialog(page: Page, username: string, password = USER_PASSWORD): Promise<void> {
  await page.goto('/');
  await page.getByRole('button', { name: 'Log in' }).click();
  await page.getByRole('tab', { name: 'Sign up' }).click();
  await page.getByRole('dialog').getByLabel('Username').fill(username);
  await page.getByRole('dialog').getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign up' }).click();
  await expect(page.getByRole('dialog')).toBeHidden();
  await expect(page.getByRole('button', { name: username })).toBeVisible();
}

/** Opens the account dialog of the landing page and logs in through it. */
export async function logInThroughTheDialog(page: Page, username: string, password = USER_PASSWORD): Promise<void> {
  await page.goto('/');
  await page.getByRole('button', { name: 'Log in' }).click();
  await page.getByRole('dialog').getByLabel('Username').fill(username);
  await page.getByRole('dialog').getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Log in' }).click();
  await expect(page.getByRole('dialog')).toBeHidden();
  await expect(page.getByRole('button', { name: username })).toBeVisible();
}

/** The username button drops a menu: "Account settings" opens the dialog with the account's options. */
export async function openAccountSettings(page: Page, username: string): Promise<void> {
  await page.getByRole('button', { name: username }).click();
  await page.getByRole('menuitem', { name: 'Account settings' }).click();
  await expect(page.getByRole('dialog', { name: 'Account settings' })).toBeVisible();
}

/** Logs out through the menu of the username button; resolves once the page offers to log in again. */
export async function logOutFromTheMenu(page: Page, username: string): Promise<void> {
  await page.getByRole('button', { name: username }).click();
  await page.getByRole('menuitem', { name: 'Log out' }).click();
  await expect(page.getByRole('button', { name: 'Log in' })).toBeVisible();
}
