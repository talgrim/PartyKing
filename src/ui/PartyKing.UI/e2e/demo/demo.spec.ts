import { expect, Page, test } from '@playwright/test';
import { DEMO_API_URL } from '../constants';

// A 1x1 transparent PNG
const PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==',
  'base64',
);

const startDemo = async (page: Page): Promise<string> => {
  await page.goto('/');
  await page.getByRole('button', { name: 'Start the demo' }).click();
  await expect(page).toHaveURL(/\/s\/[0-9a-f-]{36}$/);
  await expect(page.getByRole('heading', { name: 'Demo party' })).toBeVisible();
  return page.url().split('/s/')[1];
};

test('the landing page offers to start a demo, with no list of sessions and no accounts', async ({ page }) => {
  await page.goto('/');

  await expect(page.getByRole('heading', { name: 'Try PartyKing' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Start the demo' })).toBeVisible();
  await expect(page.getByRole('textbox')).toHaveCount(0);
  // The header keeps the calculator, but not the account menu
  await expect(page.getByRole('button', { name: 'BAC calculator' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Log in' })).toHaveCount(0);

  const config = await page.request.get(`${DEMO_API_URL}/demo/config`);
  expect((await config.json()).enabled).toBe(true);
});

test('a returning visitor sees the demos they started and hosts an older one again', async ({ page }) => {
  const first = await startDemo(page);
  // Another start replaces the host cookie, so only the second session is hosted by it
  const second = await startDemo(page);
  expect(second).not.toBe(first);

  await page.goto('/');

  await expect(page.getByRole('heading', { name: 'Your demo parties' })).toBeVisible();
  const open = page.getByRole('button', { name: 'Back to the party' });
  await expect(open).toHaveCount(2);
  await expect(page.getByText(/Ends in (3:00:00|2:59:\d\d)/).first()).toBeVisible();

  // The list is the visitor's own: the server reads it from the browser's cookie
  const mine = await page.request.get(`${DEMO_API_URL}/demo/sessions/mine`);
  expect((await mine.json()).sessions).toHaveLength(2);

  // The older session is the one that ends first, so it is the last row
  await open.last().click();
  await expect(page).toHaveURL(new RegExp(`/s/${first}$`));
  await expect(page.getByRole('heading', { name: 'Demo party' })).toBeVisible();
  await expect(page.getByText('Current Host: Demo host')).toBeVisible();
  // Host-only controls are back
  await expect(page.getByRole('button', { name: 'Session settings' })).toBeVisible();

  // A visitor who never started a demo has no list and cannot resume anyone else's
  const stranger = await page.context().browser()!.newContext();
  const strangerPage = await stranger.newPage();
  await strangerPage.goto('/');
  await expect(strangerPage.getByRole('button', { name: 'Start the demo' })).toBeVisible();
  await expect(strangerPage.getByRole('heading', { name: 'Your demo parties' })).toHaveCount(0);
  const resume = await strangerPage.request.post(`${DEMO_API_URL}/demo/sessions/${first}/resume`);
  expect(resume.status()).toBe(404);
  await stranger.close();
});

test('a host whose access token cookie is gone is still the host, the server hands out the placeholder again', async ({
  page,
}) => {
  const sessionId = await startDemo(page);
  const cookieNames = async () => (await page.context().cookies()).map((x) => x.name);
  // A start sets no access token cookie: the host handler does when the first host request needs it
  await page.reload();
  await expect(page.getByRole('button', { name: 'Session settings' })).toBeVisible();

  await page.context().clearCookies({ name: 'cookie.partyKing.auth' });
  expect(await cookieNames()).not.toContain('cookie.partyKing.auth');

  const token = await page.request.get(`${DEMO_API_URL}/spotify/token`);
  expect(token.status()).toBe(200);
  expect(await cookieNames()).toContain('cookie.partyKing.auth');

  await page.goto(`/s/${sessionId}`);
  await expect(page.getByRole('button', { name: 'Session settings' })).toBeVisible();
});

test('a visitor hosts a demo: banner, a sample track, no Spotify, and a guest joins by QR and plays', async ({
  browser,
  page,
}) => {
  const sessionId = await startDemo(page);

  // The banner counts down from about three hours, and the visitor is the host with no Spotify login to offer
  await expect(page.getByText(/Ends in (3:00:00|2:59:\d\d)/)).toBeVisible();
  await expect(page.getByText('Current Host: Demo host')).toBeVisible();
  await expect(page.getByRole('button', { name: "I'm the host" })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Log in as host with Spotify' })).toHaveCount(0);

  // The demo has no list of sessions, so nobody sees anyone else's
  const sessions = await page.request.get(`${DEMO_API_URL}/sessions`);
  expect(await sessions.json()).toEqual([]);

  // A sample track queues with one tap, and cannot be queued twice
  const sample = page.getByRole('button', { name: '+ Disfigure - Blank' });
  await sample.click();
  await expect(page.getByText('Added to the queue')).toBeVisible();
  await expect(sample).toBeDisabled();

  // Spotify is refused by the server too, not only hidden
  const search = await page.request.get(`${DEMO_API_URL}/sessions/${sessionId}/tracks?query=party`);
  expect(search.status()).toBe(403);

  // A guest scans the QR code (the dialog shows an image, so the token is read from its response)
  const created = page.waitForResponse((response) => response.url().endsWith(`/sessions/${sessionId}/join-token`));
  await page.getByRole('button', { name: 'Show QR code' }).click();
  const { token: joinToken } = await (await created).json();
  const context = await browser.newContext();
  const guest = await context.newPage();
  await guest.goto(`/s/${sessionId}?join=${encodeURIComponent(joinToken)}`);
  await expect(guest.getByRole('heading', { name: 'Demo party' })).toBeVisible();
  // Guests see the banner too, but no host controls
  await expect(guest.getByText(/Ends in /)).toBeVisible();
  await expect(guest.getByRole('button', { name: 'Session settings' })).toHaveCount(0);

  // The host opens a Ringo game and the guest joins it
  await page.goto(`/s/${sessionId}/games`);
  await page.getByRole('button', { name: 'Start new' }).click();
  await expect(page.getByRole('button', { name: 'Face down card' }).first()).toBeVisible();
  await page.getByRole('button', { name: 'Open the game' }).click();

  await guest.goto(`/s/${sessionId}/games/ringo/join`);
  await guest.getByLabel('Your name').fill('Guest');
  await guest.getByRole('button', { name: 'Join' }).click();
  await expect(page.getByText('Guest').first()).toBeVisible();
  await context.close();
});

test('a demo holds at most ten photos', async ({ page }) => {
  const sessionId = await startDemo(page);

  await page.getByRole('button', { name: 'Upload photos' }).click();
  const photos = Array.from({ length: 11 }, (_, index) => ({
    name: `dot-${index}.png`,
    mimeType: 'image/png',
    buffer: PNG,
  }));
  await page.getByLabel('Choose photos').setInputFiles(photos);
  await page.getByRole('button', { name: 'Upload 11 photos' }).click();

  await expect(page.getByText('Photos uploaded')).toBeHidden();
  const current = await page.request.get(`${DEMO_API_URL}/sessions/${sessionId}/photos/current`);
  expect(current.status()).toBe(204);
});

test('signing up is refused in a demo', async ({ request }) => {
  const response = await request.post(`${DEMO_API_URL}/account/register`, {
    data: { username: 'demo-visitor', password: 'a-long-enough-password' },
  });

  expect(response.status()).toBe(403);
});

test('when the time is up the session is replaced by the end screen, and a new demo can start', async ({ page }) => {
  // The browser's clock is the one that counts down, so the end is reached by moving it, not by waiting three hours.
  // (The server side of the end, deleting the session, is covered by the DemoSessionCleanup tests.)
  await page.clock.install();
  const firstId = await startDemo(page);
  await expect(page.getByText(/Ends in /)).toBeVisible();

  await page.clock.fastForward('03:01:00');

  await expect(page.getByRole('heading', { name: 'The demo has ended' })).toBeVisible();
  await expect(page.getByText(/Ends in /)).toHaveCount(0);

  await page.getByRole('button', { name: 'Start a new demo' }).click();
  // The end screen is on the old address until the new session opens
  await expect(page).not.toHaveURL(new RegExp(firstId));
  await expect(page).toHaveURL(/\/s\/[0-9a-f-]{36}$/);
  await expect(page.getByRole('heading', { name: 'Demo party' })).toBeVisible();
  await expect(page.getByText(/Ends in /)).toBeVisible();
});
