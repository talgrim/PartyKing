import { Page } from '@playwright/test';
import { expect, test } from './fixtures/test';

const joinRingo = async (guest: Page, sessionId: string, name: string) => {
  await guest.goto(`/s/${sessionId}/games/ringo/join`);
  await guest.getByLabel('Your name').fill(name);
  await guest.getByRole('button', { name: 'Join' }).click();
};

test('guests join a Ringo game and only the player on turn flips a card', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const admin = await openAdmin(session);

  await admin.goto(`/s/${session.id}/games`);
  await admin.getByRole('button', { name: 'Start new' }).click();
  await expect(admin.getByRole('button', { name: 'Face down card' }).first()).toBeVisible();
  await admin.getByRole('button', { name: 'Open the game' }).click();

  const anna = await openGuest(session);
  await joinRingo(anna, session.id, 'Anna');
  const bartek = await openGuest(session);
  await joinRingo(bartek, session.id, 'Bartek');

  // The admin's board shows who is playing without a reload (SignalR push)
  await expect(admin.getByText('Anna').first()).toBeVisible();
  await expect(admin.getByText('Bartek').first()).toBeVisible();

  // Anna joined first, so it is her turn: Bartek's cards are locked, hers are not
  await expect(anna.getByRole('button', { name: 'Face down card' }).first()).toBeEnabled();
  await expect(bartek.getByRole('button', { name: 'Face down card' }).first()).toBeDisabled();
  // The cards are scattered at random and overlap on purpose, so a real click on a given card can land on its
  // neighbour or be blocked by it. Any card will do, so send the click to the element itself.
  await anna.getByRole('button', { name: 'Face down card' }).first().dispatchEvent('click');
  await expect(anna.getByRole('button', { name: 'Continue' })).toBeVisible();

  // The flipped card is on everybody's screen, and the history names who flipped it
  await expect(bartek.getByRole('button', { name: 'Continue' })).toBeHidden();
  await expect(bartek.getByText(/Anna/).first()).toBeVisible();

  await anna.getByRole('button', { name: 'Continue' }).click();
  await expect(anna.getByRole('button', { name: 'Continue' })).toBeHidden();
});
