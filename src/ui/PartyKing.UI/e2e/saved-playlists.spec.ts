import { Page, expect } from '@playwright/test';
import { API_URL } from './constants';
import { signUpThroughTheDialog, uniqueUsername } from './fixtures/accounts';
import { CreatedSession, test } from './fixtures/test';

// Embeddable videos; when YouTube cannot be reached the server queues the link under its URL
const SONGS = ['https://www.youtube.com/watch?v=jNQXAC9IVRw', 'https://www.youtube.com/watch?v=dQw4w9WgXcQ'];

/** Queues a song through the API with the cookies of the page, which has unlocked the session. */
async function queueSong(page: Page, session: CreatedSession, input: string): Promise<void> {
  const response = await page.context().request.post(`${API_URL}/sessions/${session.id}/songs`, { data: { input } });
  expect(response.ok()).toBeTruthy();
}

test('a guest without an account has no save-as-playlist button, and the API turns them away', async ({
  createSession,
  openGuest,
}) => {
  const session = await createSession();
  const guest = await openGuest(session);

  await expect(guest.getByRole('heading', { name: session.name })).toBeVisible();
  await expect(guest.getByRole('button', { name: 'Save as playlist' })).toBeHidden();

  const response = await guest
    .context()
    .request.post(`${API_URL}/sessions/${session.id}/saved-playlists`, { data: { name: 'Mine', sections: [2] } });
  expect(response.status()).toBe(401);
});

test('a signed-in guest saves the queue as a playlist', async ({ createSession, openGuest }) => {
  const session = await createSession();
  const guest = await openGuest(session);
  await queueSong(guest, session, SONGS[0]);
  await queueSong(guest, session, SONGS[1]);
  await queueSong(guest, session, SONGS[0]);

  await signUpThroughTheDialog(guest, uniqueUsername('lister'));
  await guest.goto(`/s/${session.id}`);
  await guest.getByRole('button', { name: 'Save as playlist' }).click();

  const dialog = guest.getByRole('dialog', { name: 'Save as playlist' });
  await expect(dialog.getByRole('textbox', { name: 'Playlist name' })).toHaveValue(session.name);

  // Nothing plays and nothing was played, so only the queue can be saved; the song queued twice counts twice
  await expect(dialog.getByRole('checkbox', { name: /Up next/ })).toBeChecked();
  await expect(dialog.getByLabel('3 songs')).toBeVisible();
  await expect(dialog.getByRole('checkbox', { name: /Playing now/ })).toBeDisabled();
  await expect(dialog.getByRole('checkbox', { name: /Played earlier/ })).toBeDisabled();
  await expect(dialog.getByRole('checkbox', { name: /fallback playlist/ })).toBeDisabled();

  await dialog.getByRole('textbox', { name: 'Playlist name' }).fill('Friday mix');
  await dialog.getByRole('button', { name: 'Save playlist' }).click();

  // The song queued twice is saved once
  await expect(dialog.getByRole('status')).toHaveText('Saved 2 songs as “Friday mix”.');
  await dialog.getByRole('button', { name: 'Close' }).first().click();
  await expect(dialog).toBeHidden();
});

test('a session without songs has nothing to save', async ({
  createSession,
  openGuest,
}) => {
  const session = await createSession();
  const guest = await openGuest(session);
  await signUpThroughTheDialog(guest, uniqueUsername('empty'));
  await guest.goto(`/s/${session.id}`);

  await guest.getByRole('button', { name: 'Save as playlist' }).click();

  const dialog = guest.getByRole('dialog', { name: 'Save as playlist' });
  await expect(dialog.getByLabel('0 songs').first()).toBeVisible();
  await expect(dialog.getByRole('button', { name: 'Save playlist' })).toBeDisabled();
});
