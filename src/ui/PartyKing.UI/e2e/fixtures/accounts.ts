import { APIRequestContext, Page, expect, request } from '@playwright/test';
import { API_URL, IDENTITY_URL } from '../constants';

export const USER_PASSWORD = 'user-password-1';

let counter = 0;

/** A username no other test uses; the (fake) identity service keeps its accounts for the whole run. */
export const uniqueUsername = (prefix = 'user') => `${prefix}-${Date.now().toString(36)}-${counter++}`;

/**
 * Registers a user at the identity service; the returned context talks to the API and carries the new session cookie
 * (it has no Domain, so a context sends it to every port of 127.0.0.1).
 */
export async function registerUser(username: string, password = USER_PASSWORD): Promise<APIRequestContext> {
  const api = await request.newContext({ baseURL: API_URL });
  const response = await api.post(`${IDENTITY_URL}/api/register`, { data: { username, password } });
  expect(response.status()).toBe(204);

  return api;
}

/** The id the identity service gave the account the context is signed in as. */
export async function accountIdOf(api: APIRequestContext): Promise<string> {
  const response = await api.get(`${IDENTITY_URL}/api/me`);
  expect(response.ok()).toBeTruthy();

  return (await response.json()).id as string;
}

/** Follows the header's "Sign up" link to the identity service's page and signs up there; ends back on the landing page. */
export async function signUpThroughIdentity(page: Page, username: string, password = USER_PASSWORD): Promise<void> {
  await page.goto('/');
  await page.getByRole('link', { name: 'Sign up' }).click();
  await page.getByLabel('Username').fill(username);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign up' }).click();
  await expect(page.getByRole('button', { name: username })).toBeVisible();
}

/** Follows the header's "Log in" link to the identity service's page and logs in there; ends back where it started. */
export async function logInThroughIdentity(page: Page, username: string, password = USER_PASSWORD): Promise<void> {
  await page.goto('/');
  await page.getByRole('link', { name: 'Log in' }).click();
  await page.getByLabel('Username').fill(username);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Log in' }).click();
  await expect(page.getByRole('button', { name: username })).toBeVisible();
}

/** Logs out through the menu of the username button; resolves once the page offers to log in again. */
export async function logOutFromTheMenu(page: Page, username: string): Promise<void> {
  await page.getByRole('button', { name: username }).click();
  await page.getByRole('menuitem', { name: 'Log out' }).click();
  await expect(page.getByRole('link', { name: 'Log in' })).toBeVisible();
}
