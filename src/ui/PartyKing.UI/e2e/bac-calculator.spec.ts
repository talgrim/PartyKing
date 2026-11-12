import { expect, test } from '@playwright/test';

test('a visitor estimates their blood alcohol in the header calculator, which remembers them', async ({ page }) => {
  await page.goto('/');
  await page.getByRole('button', { name: 'BAC calculator' }).click();

  const dialog = page.getByRole('dialog', { name: 'BAC calculator' });
  await expect(dialog.getByText('Enter your weight to see the estimate.')).toBeVisible();

  // The warning is behind the "i" next to the close button; Escape closes it and leaves the calculator open
  await dialog.getByRole('button', { name: 'About the estimate' }).click();
  await expect(page.getByText(/Never use it to decide whether you can drive/)).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByText(/Never use it to decide whether you can drive/)).toBeHidden();
  await expect(dialog).toBeVisible();

  // Without a weight, "About you" is open and "Drinks" cannot be opened
  await expect(dialog.getByRole('button', { name: 'Drinks', exact: true })).toBeDisabled();
  await dialog.getByLabel('Weight (kg)').fill('80');
  await dialog.getByRole('button', { name: 'Drinks', exact: true }).click();
  await dialog.getByLabel('Minutes ago').fill('60');
  await dialog.getByRole('button', { name: 'Add drink' }).click();
  await expect(dialog.getByText(/500 ml · 5%/)).toBeVisible();
  await expect(dialog.getByText('19.7 g of alcohol')).toBeVisible();

  const result = dialog.getByRole('region', { name: 'Estimate' });
  await expect(result).toBeVisible();
  await expect(result.getByText('0.000 %', { exact: true })).toBeHidden();
  await expect(result.getByText(/Driving limit \(US\): 0\.080 %/)).toBeVisible();

  await page.reload();
  await page.getByRole('button', { name: 'BAC calculator' }).click();
  await expect(page.getByLabel('Weight (kg)')).toHaveValue('80');
  await expect(page.getByText(/500 ml · 5%/)).toBeVisible();
});

test('numbers change with the mouse wheel, and an entry can be edited, repeated and deleted', async ({ page }) => {
  await page.goto('/');
  await page.getByRole('button', { name: 'BAC calculator' }).click();
  const dialog = page.getByRole('dialog', { name: 'BAC calculator' });

  // The wheel only reaches the focused field, and the dialog does not scroll under it
  const weight = dialog.getByLabel('Weight (kg)');
  await weight.fill('80');
  await weight.hover();
  await page.mouse.wheel(0, -100);
  await expect(weight).toHaveValue('81');
  await page.mouse.wheel(0, 100);
  await page.mouse.wheel(0, 100);
  await expect(weight).toHaveValue('79');

  await dialog.getByRole('button', { name: 'Drinks', exact: true }).click();
  await dialog.getByRole('button', { name: 'Add drink' }).click();
  await expect(dialog.getByText(/500 ml · 5%/)).toBeVisible();
  await expect(dialog.getByLabel('Beer: Volume (ml)')).toBeHidden();

  await dialog.getByRole('button', { name: 'Edit Beer' }).click();
  await dialog.getByLabel('Beer: Volume (ml)').fill('330');
  await dialog.getByRole('button', { name: 'Save Beer' }).click();
  await expect(dialog.getByText(/330 ml · 5%/)).toBeVisible();

  await dialog.getByRole('button', { name: 'Add another Beer now' }).click();
  await expect(dialog.getByText(/330 ml · 5%/)).toHaveCount(2);

  await dialog.getByRole('button', { name: 'Remove Beer' }).first().click();
  await expect(dialog.getByText(/330 ml · 5%/)).toHaveCount(1);
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
