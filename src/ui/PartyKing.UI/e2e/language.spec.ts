import { expect, test } from '@playwright/test';

test('the language switch changes the texts and is remembered', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Join a party' })).toBeVisible();

  // The switch is labelled in the current language, so find its buttons by their fixed names
  const polish = page.getByRole('button', { name: 'Polski' });
  const english = page.getByRole('button', { name: 'English' });
  await polish.click();
  await expect(page.getByRole('heading', { name: 'Join a party' })).toBeHidden();
  await expect(polish).toHaveAttribute('aria-pressed', 'true');

  await page.reload();
  await expect(polish).toHaveAttribute('aria-pressed', 'true');

  await english.click();
  await expect(page.getByRole('heading', { name: 'Join a party' })).toBeVisible();
});
