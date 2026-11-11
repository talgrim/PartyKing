import { APIRequestContext, Page, expect } from '@playwright/test';
import { API_URL } from './constants';
import { signUpThroughTheDialog, uniqueUsername } from './fixtures/accounts';
import { CreatedSession, test } from './fixtures/test';

// Embeddable videos; when YouTube cannot be reached the server queues the link under its URL
const SONGS = ['https://www.youtube.com/watch?v=jNQXAC9IVRw', 'https://www.youtube.com/watch?v=dQw4w9WgXcQ'];

let counter = 0;
const uniqueName = () => `Mix ${Date.now().toString(36)}-${counter++}`;

type SavedPlaylist = { id: string; name: string; songCount: number };
type QueueItem = { url: string; isFallback: boolean };

/** Queues songs through the API with the cookies of the page, which has unlocked the session. */
async function queueSongs(page: Page, session: CreatedSession, inputs: string[]): Promise<void> {
  for (const input of inputs) {
    const response = await page.context().request.post(`${API_URL}/sessions/${session.id}/songs`, { data: { input } });
    expect(response.ok()).toBeTruthy();
  }
}

/** Saves the queue of the session as a playlist of the account the page is signed in as. */
async function savePlaylist(page: Page, session: CreatedSession, name: string): Promise<SavedPlaylist> {
  const response = await page
    .context()
    .request.post(`${API_URL}/sessions/${session.id}/saved-playlists`, { data: { name, sections: [2] } });
  expect(response.ok()).toBeTruthy();

  return (await response.json()) as SavedPlaylist;
}

/**
 * The admin cookies come from the API, so this browser has no localStorage hint that it holds a login, and the page
 * would take the admin for a visitor (as it does for anybody who cleared their site data).
 */
async function rememberTheLogin(page: Page): Promise<void> {
  await page.evaluate(() => localStorage.setItem('partyking.accountHint', 'true'));
  await page.reload();
}

const setFallback = (api: APIRequestContext, session: CreatedSession, playlistId: string) =>
  api.put(`${API_URL}/sessions/${session.id}/fallback-saved-playlist`, { data: { playlistId, shuffle: false } });

async function queueOf(page: Page, session: CreatedSession): Promise<QueueItem[]> {
  const response = await page.context().request.get(`${API_URL}/sessions/${session.id}/songs`);
  expect(response.ok()).toBeTruthy();

  return (await response.json()) as QueueItem[];
}

test('the admin plays a playlist they saved when the queue of another session is empty', async ({
  createSession,
  openAdmin,
}) => {
  const source = await createSession();
  const target = await createSession();
  const name = uniqueName();
  const sourcePage = await openAdmin(source);
  await queueSongs(sourcePage, source, SONGS);
  await savePlaylist(sourcePage, source, name);
  const admin = await openAdmin(target);
  await rememberTheLogin(admin);
  expect(await queueOf(admin, target)).toEqual([]);

  await admin.getByRole('button', { name: 'Session settings' }).click();
  const settings = admin.getByRole('dialog', { name: 'Session settings' });
  const fallback = settings.getByRole('region', { name: 'Fallback playlist' });
  const saved = fallback.getByRole('list', { name: 'Saved playlists' });
  await expect(saved.getByRole('button', { name: new RegExp(name) })).toContainText('2 songs');
  await expect(fallback.getByRole('button', { name: 'Save' })).toBeDisabled();

  await saved.getByRole('button', { name: new RegExp(name) }).click();
  await fallback.getByRole('button', { name: 'Save' }).click();

  await expect(fallback.getByText('Fallback playlist saved')).toBeVisible();
  await expect(fallback.getByText('Plays when the queue is empty')).toBeVisible();
  await expect(fallback.getByTitle(name)).toBeVisible();
  await expect(saved.getByRole('button', { name: new RegExp(name) })).toContainText('in use');

  // Choosing the playlist queues its first song at once
  await expect
    .poll(async () => (await queueOf(admin, target)).map((x) => ({ url: x.url, isFallback: x.isFallback })))
    .toEqual([{ url: SONGS[0], isFallback: true }]);

  const session = await admin.context().request.get(`${API_URL}/sessions/${target.id}`);
  expect((await session.json()).fallbackPlaylistName).toBe(name);

  await fallback.getByRole('button', { name: 'Remove' }).click();

  await expect(fallback.getByText('Fallback playlist removed')).toBeVisible();
  await expect(fallback.getByText('Plays when the queue is empty')).toBeHidden();
});

test('only a manager signed in as the owner can choose a saved playlist', async ({
  createSession,
  openGuest,
  openAdmin,
}) => {
  const session = await createSession();
  const owner = await openGuest(session);
  await signUpThroughTheDialog(owner, uniqueUsername('owner'));
  await queueSongs(owner, session, [SONGS[0]]);
  const playlist = await savePlaylist(owner, session, uniqueName());
  const admin = await openAdmin(session);
  const visitor = await openGuest(session);

  // Not a host or an admin, however much the playlist is theirs
  expect((await setFallback(owner.context().request, session, playlist.id)).status()).toBe(403);
  // A manager, but the playlist is somebody else's
  expect((await setFallback(admin.context().request, session, playlist.id)).status()).toBe(404);
  // No account at all
  expect((await setFallback(visitor.context().request, session, playlist.id)).status()).toBe(401);

  const response = await admin.context().request.get(`${API_URL}/sessions/${session.id}`);
  expect((await response.json()).fallbackSavedPlaylistId ?? null).toBeNull();
});
