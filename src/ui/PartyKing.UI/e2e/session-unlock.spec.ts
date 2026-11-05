import { expect, test } from './fixtures/test';

test('a session opens only with its password', async ({ page, createSession }) => {
  const session = await createSession();

  await page.goto(`/s/${session.id}`);
  await expect(page.getByRole('heading', { name: 'This session is password protected' })).toBeVisible();

  await page.getByLabel('Session password').fill('definitely-wrong');
  await page.getByRole('button', { name: 'Join' }).click();
  await expect(page.getByText('Wrong password')).toBeVisible();

  await page.getByLabel('Session password').fill(session.password);
  await page.getByRole('button', { name: 'Join' }).click();
  await expect(page.getByRole('heading', { name: 'This session is password protected' })).toBeHidden();
  await expect(page.getByText(session.name)).toBeVisible();

  // The unlock is a cookie, so it survives a reload
  await page.reload();
  await expect(page.getByText(session.name)).toBeVisible();
  await expect(page.getByLabel('Session password')).toBeHidden();
});
