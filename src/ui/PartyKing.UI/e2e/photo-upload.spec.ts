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

/** A JPEG segment: the marker, the length (which counts its own two bytes) and the payload. */
function segment(marker: number, payload: Buffer): Buffer {
  return Buffer.concat([Buffer.from([0xff, marker, (payload.length + 2) >> 8, (payload.length + 2) & 0xff]), payload]);
}

test('the location in a photo is not stored', async ({ page, createSession }) => {
  const session = await createSession();
  await unlockSession(page, session);

  // Not a real picture: the server only walks the segments. The Exif and the XMP segments hold the "location".
  const jpeg = Buffer.concat([
    Buffer.from([0xff, 0xd8]),
    segment(0xe1, Buffer.from('Exif\0\0MM\0*\0\0\0\x08GPS-SENTINEL-EXIF', 'latin1')),
    segment(0xe1, Buffer.from('http://ns.adobe.com/xap/1.0/\0<x:xmpmeta>GPS-SENTINEL-XMP</x:xmpmeta>', 'latin1')),
    segment(0xdb, Buffer.from([0, 16, 11, 12, 14])),
    segment(0xc0, Buffer.from([8, 0, 1, 0, 1, 1, 1, 0x11, 0])),
    segment(0xc4, Buffer.from([0, 1, 0, 0, 0])),
    segment(0xda, Buffer.from([1, 1, 0, 0, 0x3f, 0])),
    Buffer.from([0x12, 0x34, 0x56]),
    Buffer.from([0xff, 0xd9]),
  ]);

  const upload = await page.request.put(`${API_URL}/sessions/${session.id}/photos/false`, {
    multipart: { files: { name: 'located.jpg', mimeType: 'image/jpeg', buffer: jpeg } },
  });
  expect(upload.ok()).toBe(true);

  const current = await page.request.get(`${API_URL}/sessions/${session.id}/photos/current`);
  expect(current.status()).toBe(200);
  const { fileName } = (await current.json()) as { fileName: string };

  const stored = await page.request.get(`${API_URL}/${fileName}`);
  const body = await stored.body();
  expect(body.includes('GPS-SENTINEL')).toBe(false);
  expect(body.includes('Exif')).toBe(false);
  expect(body.subarray(0, 2)).toEqual(Buffer.from([0xff, 0xd8]));
  expect(body.includes(Buffer.from([0x12, 0x34, 0x56]))).toBe(true);
});
