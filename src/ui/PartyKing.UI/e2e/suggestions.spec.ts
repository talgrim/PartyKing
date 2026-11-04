import { expect, test } from './fixtures/test';

// The e2e API has no LLM key on purpose, so the suggestion endpoints answer from their built-in lists. This guards
// the fix for the 500 that every request got when the key was blank.
test('Never have I ever suggests statements from the built-in list when the AI is unavailable', async ({
  createSession,
  openAdmin,
}) => {
  const session = await createSession();
  const admin = await openAdmin(session);

  await admin.goto(`/s/${session.id}/games`);
  await admin.getByRole('button', { name: 'Suggest statements' }).click();

  await expect(
    admin.getByText('The AI is unavailable, here are statements from the built-in list.'),
  ).toBeVisible();
  const statements = admin.getByRole('listitem').filter({ hasText: /^Never have I ever/i });
  await expect(statements.first()).toBeVisible();
  await expect(admin.getByText('Could not get suggestions')).toBeHidden();
});

test('an ace in Ringo offers rule suggestions from the built-in list when the AI is unavailable', async ({
  createSession,
  openAdmin,
}) => {
  const session = await createSession();
  const admin = await openAdmin(session);

  await admin.goto(`/s/${session.id}/games`);
  await admin.getByRole('button', { name: 'Start new' }).click();
  await expect(admin.getByRole('button', { name: 'Face down card' }).first()).toBeEnabled();

  // The deck is shuffled by a random seed, so flip cards (the admin may flip freely while nobody has joined) until
  // an ace turns up. Only if all four kings came before the first ace (about 1 game in 70) would the game end first,
  // and the retry CI does would then run another deck.
  const suggest = admin.getByRole('button', { name: 'Suggest rules' });
  for (let flipped = 0; flipped < 48 && !(await suggest.isVisible()); flipped++) {
    // The cards overlap at random, so send the click to the element instead of through the pointer
    await admin.getByRole('button', { name: 'Face down card' }).first().dispatchEvent('click');
    await expect(admin.getByRole('button', { name: 'Continue' })).toBeVisible();
    if (await suggest.isVisible()) break;
    await admin.getByRole('button', { name: 'Continue' }).click();
    await expect(admin.getByRole('button', { name: 'Continue' })).toBeHidden();
  }
  await expect(suggest).toBeVisible();

  await suggest.click();
  await expect(admin.getByText('The AI is unavailable, here are rules from the built-in list.')).toBeVisible();
  await expect(admin.getByRole('dialog').getByRole('listitem')).toHaveCount(5);
  await expect(admin.getByText('Could not get suggestions')).toBeHidden();
});
