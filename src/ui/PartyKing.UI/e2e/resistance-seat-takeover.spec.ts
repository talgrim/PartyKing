import { startResistanceGame } from './fixtures/resistance';
import { expect, test } from './fixtures/test';

test('a seat of an offline player is released and taken over from a new device', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const { admin, guests } = await startResistanceGame(session, { openAdmin, openGuest });

  // Ela loses her device (the tab is gone); presence turns offline after a short grace period
  await guests.get('Ela')!.context().close();

  await admin.getByRole('button', { name: 'Free a seat' }).click();
  const dialog = admin.getByRole('dialog', { name: 'Free a seat' });
  const release = dialog.getByRole('button', { name: 'Release the seat of Ela' });
  await expect(release).toBeEnabled({ timeout: 30_000 });

  // The others are online, so their seats cannot be released
  await expect(dialog.getByRole('button', { name: 'Release the seat of Anna' })).toBeDisabled();

  await release.click();
  await expect(dialog.getByRole('button', { name: 'Take back the release of Ela' })).toBeVisible();
  await dialog.getByRole('button', { name: 'Close' }).click();

  // A new device (no cookies of the old one) picks her name from the released seats
  const device = await openGuest(session);
  await device.goto(`/s/${session.id}/games/resistance/join`);
  await expect(device.getByText('This game has already started.')).toBeVisible();
  const picker = device.getByRole('region', { name: 'Back in the game' });
  await expect(picker.getByRole('button', { name: 'Take over: Anna' })).toBeHidden();
  await picker.getByRole('button', { name: 'Take over: Ela' }).click();

  // She is in the game again, with a role card of her own
  await expect(device.getByRole('button', { name: 'Show your role' })).toBeVisible();

  // The seat is used up: nobody else can claim it
  const another = await openGuest(session);
  await another.goto(`/s/${session.id}/games/resistance/join`);
  await expect(another.getByText('No seat is free to take over.')).toBeVisible();
});

test('a seat that was not released cannot be taken over', async ({ createSession, openAdmin, openGuest }) => {
  const session = await createSession();
  await startResistanceGame(session, { openAdmin, openGuest });

  const stranger = await openGuest(session);
  await stranger.goto(`/s/${session.id}/games/resistance/join`);
  await expect(stranger.getByText('This game has already started.')).toBeVisible();
  await expect(stranger.getByText('No seat is free to take over.')).toBeVisible();
});
