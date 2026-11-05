import { expect, test } from '@playwright/test';

test('the home page loads the sessions from the API', async ({ page }) => {
  await page.goto('/');

  await expect(page.getByRole('heading', { name: 'Join a party' })).toBeVisible();
  await expect(page.getByLabel('Search sessions by code')).toBeVisible();
  await expect(page.getByText('Could not load the sessions')).toBeHidden();
});
