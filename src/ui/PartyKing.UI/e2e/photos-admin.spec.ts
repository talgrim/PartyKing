import { expect, test } from './fixtures/test';

// A 1x1 transparent PNG
const PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==',
  'base64',
);

test('an admin sees the uploaded photo in the slideshow, the photo count and downloads the zip', async ({
  createSession,
  openAdmin,
}) => {
  const session = await createSession();
  const admin = await openAdmin(session);

  // Before any upload: an empty slideshow and a disabled archive link
  await expect(admin.getByRole('heading', { name: 'No photos yet' })).toBeVisible();
  await admin.getByRole('button', { name: 'Session settings' }).click();
  const photos = admin.getByRole('region', { name: 'Photos' });
  await expect(photos.getByText('No photos have been uploaded yet.')).toBeVisible();
  await expect(photos.getByRole('link', { name: 'Download as ZIP' })).toHaveAttribute('aria-disabled', 'true');
  await admin.getByRole('button', { name: 'Close' }).click();

  await admin.getByRole('button', { name: 'Upload photos' }).click();
  await admin.getByLabel('Choose photos').setInputFiles({ name: 'dot.png', mimeType: 'image/png', buffer: PNG });
  await admin.getByRole('button', { name: 'Upload 1 photo' }).click();
  await expect(admin.getByText('Photos uploaded')).toBeVisible();
  await admin.getByRole('dialog').getByRole('button', { name: 'Close' }).click();

  // The slideshow picks the photo up (the upload rotates an empty slideshow at once)
  await expect(admin.locator('.photos-slideshow img').first()).toBeAttached();

  await admin.getByRole('button', { name: 'Session settings' }).click();
  await expect(photos.getByText('1 photo is stored for this session.')).toBeVisible();
  const link = photos.getByRole('link', { name: 'Download as ZIP' });
  await expect(link).toHaveAttribute('aria-disabled', 'false');

  const [download] = await Promise.all([admin.waitForEvent('download'), link.click()]);
  expect(download.suggestedFilename()).toMatch(/\.zip$/i);
});
