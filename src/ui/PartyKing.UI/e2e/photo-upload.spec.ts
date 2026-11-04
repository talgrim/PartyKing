import { API_URL } from './constants';
import { expect, test, unlockSession } from './fixtures/test';

// A 1x1 transparent PNG
const PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==',
  'base64',
);

test('a guest uploads a photo into the session slideshow', async ({ page, createSession }) => {
  const session = await createSession();

  await unlockSession(page, session);

  // Nothing is uploaded yet: the API answers 204
  const before = await page.request.get(`${API_URL}/sessions/${session.id}/photos/current`);
  expect(before.status()).toBe(204);

  await page.getByRole('button', { name: 'Upload photos' }).click();
  await page.getByLabel('Choose photos').setInputFiles({ name: 'dot.png', mimeType: 'image/png', buffer: PNG });
  await page.getByRole('button', { name: 'Upload 1 photo' }).click();
  await expect(page.getByText('Photos uploaded')).toBeVisible();

  await expect
    .poll(async () => (await page.request.get(`${API_URL}/sessions/${session.id}/photos/current`)).status())
    .toBe(200);
});

test('a file that is not an image is refused', async ({ page, createSession }) => {
  const session = await createSession();

  await unlockSession(page, session);

  await page.getByRole('button', { name: 'Upload photos' }).click();
  await page.getByLabel('Choose photos').setInputFiles({
    name: 'fake.png',
    mimeType: 'image/png',
    buffer: Buffer.from('this is plain text, not a PNG'),
  });
  await page.getByRole('button', { name: 'Upload 1 photo' }).click();

  await expect(page.getByText('Photos uploaded')).toBeHidden();
  const current = await page.request.get(`${API_URL}/sessions/${session.id}/photos/current`);
  expect(current.status()).toBe(204);
});
