import { expect, test } from '@playwright/test';

test('a visitor estimates their blood alcohol in the header calculator, which remembers them', async ({ page }) => {
  await page.goto('/');
  await page.getByRole('button', { name: 'BAC calculator' }).click();

  const dialog = page.getByRole('dialog', { name: 'BAC calculator' });
  await expect(dialog.getByText('Enter your weight to see the estimate.')).toBeVisible();

  await dialog.getByLabel('Weight (kg)').fill('80');
  await dialog.getByRole('button', { name: '+ Beer' }).click();
  await dialog.getByLabel('Beer: Minutes ago').fill('60');

  const result = dialog.getByRole('region', { name: 'Estimate' });
  await expect(result).toBeVisible();
  await expect(result.getByText('0.000 %', { exact: true })).toBeHidden();
  await expect(result.getByText(/Driving limit \(US\): 0\.080 %/)).toBeVisible();

  await page.reload();
  await page.getByRole('button', { name: 'BAC calculator' }).click();
  await expect(page.getByLabel('Weight (kg)')).toHaveValue('80');
  await expect(page.getByLabel('Beer: Volume (ml)')).toHaveValue('500');
});

test('the calculator shows promille and the Polish limits in Polish, and is on every page', async ({ page }) => {
  await page.goto('/admin');
  await page.getByRole('button', { name: 'Polski' }).click();

  await page.getByRole('button', { name: 'Kalkulator promili' }).click();
  const dialog = page.getByRole('dialog', { name: 'Kalkulator promili' });
  await dialog.getByLabel('Waga (kg)').fill('80');

  await expect(dialog.getByText('0,00 ‰', { exact: true })).toBeVisible();
  await expect(dialog.getByText(/Stan nietrzeźwości: 0,50 ‰/)).toBeVisible();
});
