import { uniqueUsername, signUpThroughIdentity } from './fixtures/accounts';
import { expect, test } from './fixtures/test';

// The bar at the top of every page is the one of all the talgrim.dev sites (see docs/frontend.md)

test('a visitor gets the bar and the title of the site, without the links to the other sites', async ({ page }) => {
  await page.goto('/');

  await expect(page).toHaveTitle('PartyKing · talgrim.dev');
  const bar = page.getByRole('banner');
  await expect(bar.getByRole('link', { name: 'PartyKing' })).toHaveAttribute('href', '/');
  await expect(bar.getByRole('link', { name: 'talgrim.dev' })).toBeVisible();
  await expect(bar.getByRole('navigation', { name: 'Sites' })).toHaveCount(0);
  await expect(bar.getByRole('link', { name: 'Log in' })).toBeVisible();
});

test('a signed-in user gets the links to the other sites, with this one marked', async ({ page }) => {
  await signUpThroughIdentity(page, uniqueUsername('eve'));

  const sites = page.getByRole('navigation', { name: 'Sites' });
  await expect(sites.getByRole('link', { name: 'Notes' })).toHaveAttribute('href', 'https://notes.talgrim.dev/');
  await expect(sites.getByRole('link', { name: 'PartyKing' })).toHaveAttribute('aria-current', 'page');
});

test('the bar is in the page flow, so nothing is hidden under it', async ({ page }) => {
  await page.goto('/');

  const bar = await page.getByRole('banner').boundingBox();
  const heading = await page.getByRole('heading', { name: 'Join a party' }).boundingBox();

  expect(bar!.y).toBe(0);
  expect(heading!.y).toBeGreaterThanOrEqual(bar!.y + bar!.height);
});

test('the title names the page, and the language switch changes it as well', async ({ page }) => {
  await page.goto('/admin');
  await expect(page).toHaveTitle('Admin · PartyKing · talgrim.dev');

  await page.getByRole('button', { name: 'Polski' }).click();

  await expect(page).toHaveTitle('Administrator · PartyKing · talgrim.dev');
});
