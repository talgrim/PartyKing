import { APIRequestContext, request } from '@playwright/test';
import { API_URL, IDENTITY_URL } from './constants';
import {
  accountIdOf,
  logInThroughIdentity,
  logOutFromTheMenu,
  registerUser,
  signUpThroughIdentity,
  uniqueUsername,
} from './fixtures/accounts';
import { expect, test } from './fixtures/test';

// The accounts belong to the identity service (a fake one here, e2e/fixtures/fake-identity): PartyKing links to its
// pages, forwards its session cookie and delivers its account deletion event.

test('a visitor signs up at the identity service, comes back signed in, stays so after a reload and logs out', async ({
  page,
}) => {
  const username = uniqueUsername('ann');

  await page.goto('/');
  const login = page.getByRole('link', { name: 'Log in' });
  await expect(login).toHaveAttribute('href', new RegExp(`^${IDENTITY_URL}/login\\?next=`));

  await signUpThroughIdentity(page, username);
  await page.reload();
  await expect(page.getByRole('button', { name: username })).toBeVisible();

  await logOutFromTheMenu(page, username);
  await page.reload();
  await expect(page.getByRole('link', { name: 'Log in' })).toBeVisible();
});

test('a user logs in again at the identity service and comes back to the page they left', async ({ page }) => {
  const username = uniqueUsername('bea');
  await (await registerUser(username)).dispose();

  await logInThroughIdentity(page, username);

  await expect(page).toHaveURL('/');
});

test('"Account settings" leads to the identity service', async ({ page }) => {
  const username = uniqueUsername('cy');
  await signUpThroughIdentity(page, username);

  await page.getByRole('button', { name: username }).click();
  await page.getByRole('menuitem', { name: 'Account settings' }).click();

  await expect(page).toHaveURL(`${IDENTITY_URL}/account`);
});

test('a user is turned away from /admin, and the API refuses them the admin endpoints', async ({ page }) => {
  const username = uniqueUsername('dan');
  await signUpThroughIdentity(page, username);

  await page.goto('/admin');
  await expect(page.getByText(/not an admin account/)).toBeVisible();
  await expect(page.getByLabel('New session name')).toBeHidden();

  expect((await page.context().request.get(`${API_URL}/admin/sessions`)).status()).toBe(403);
});

test('an admin of the identity service is an admin here, and a visitor is not', async ({ browser }) => {
  const visitor = await request.newContext({ baseURL: API_URL });
  expect((await visitor.get('/admin/sessions')).status()).toBe(401);
  await visitor.dispose();

  const admin = await browser.newContext({ storageState: 'e2e/.auth/admin.json' });
  expect((await admin.request.get(`${API_URL}/admin/sessions`)).status()).toBe(200);
  await admin.close();
});

test('a session cookie the identity service does not know is nobody', async () => {
  const api = await request.newContext({ baseURL: API_URL, extraHTTPHeaders: { Cookie: 'talgrim.session=made-up' } });

  expect((await api.get('/admin/sessions')).status()).toBe(401);
  await api.dispose();
});

test.describe('the account deletion event', () => {
  const callWebhook = async (accountId: string, query = ''): Promise<number> => {
    const identity = await request.newContext({ baseURL: IDENTITY_URL });
    const response = await identity.post(`/test/webhook/${accountId}${query}`);
    const { status } = (await response.json()) as { status: number };
    await identity.dispose();

    return status;
  };

  const savedPlaylists = async (api: APIRequestContext): Promise<unknown[]> => {
    const response = await api.get('/account/saved-playlists');
    expect(response.ok()).toBeTruthy();

    return (await response.json()) as unknown[];
  };

  test('deletes the saved playlists of that account, and only a signed call is believed', async ({
    createSession,
  }) => {
    const session = await createSession();
    const owner = await registerUser(uniqueUsername('eve'));
    const unlock = await owner.post(`/sessions/${session.id}/join`, { data: { password: session.password } });
    expect(unlock.ok()).toBeTruthy();
    const queued = await owner.post(`/sessions/${session.id}/songs`, {
      data: { input: 'https://www.youtube.com/watch?v=jNQXAC9IVRw' },
    });
    expect(queued.ok()).toBeTruthy();
    const saved = await owner.post(`/sessions/${session.id}/saved-playlists`, { data: { name: 'Mine', sections: [2] } });
    expect(saved.ok()).toBeTruthy();
    expect(await savedPlaylists(owner)).toHaveLength(1);
    const accountId = await accountIdOf(owner);

    // A wrong signature and an old timestamp are refused and delete nothing
    expect(await callWebhook(accountId, '?signature=bad')).toBe(401);
    expect(await callWebhook(accountId, '?ageSeconds=600')).toBe(401);
    expect(await savedPlaylists(owner)).toHaveLength(1);

    // The real thing, and a retry of it
    expect(await callWebhook(accountId)).toBe(204);
    expect(await savedPlaylists(owner)).toHaveLength(0);
    expect(await callWebhook(accountId)).toBe(204);

    await owner.dispose();
  });

  test('is a no-op for an account that saved nothing', async () => {
    const owner = await registerUser(uniqueUsername('flo'));

    expect(await callWebhook(await accountIdOf(owner))).toBe(204);
    await owner.dispose();
  });
});
