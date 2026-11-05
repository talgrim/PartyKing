import { expect, test } from './fixtures/test';

const NAMES = ['Anna', 'Bartek', 'Celina', 'Darek', 'Ela'];

test('five guests join a Resistance game and the admin starts it', async ({ createSession, openAdmin, openGuest }) => {
  const session = await createSession();
  const admin = await openAdmin(session);

  // The admin creates the lobby and opens it for guests
  await admin.goto(`/s/${session.id}/games`);
  await admin.getByRole('button', { name: 'Create' }).click();
  await expect(admin.getByText('0/10 players')).toBeVisible();
  await admin.getByRole('button', { name: 'Open the game' }).click();

  // Not enough players yet
  await expect(admin.getByRole('button', { name: 'Start game' })).toBeDisabled();

  const guests = [];
  for (const [index, name] of NAMES.entries()) {
    const guest = await openGuest(session);
    await guest.goto(`/s/${session.id}/games/resistance/join`);
    await guest.getByLabel('Your name').fill(name);
    await guest.getByRole('button', { name: 'Join' }).click();
    guests.push(guest);

    // The admin's board follows without a reload (SignalR push)
    await expect(admin.getByText(`${index + 1}/10 players`)).toBeVisible();
  }

  await expect(admin.getByRole('button', { name: 'Start game' })).toBeEnabled();
  await admin.getByRole('button', { name: 'Start game' }).click();

  // Every player gets their own secret role card; the admin, who is not playing, gets none
  for (const guest of guests) {
    await expect(guest.getByRole('button', { name: 'Show your role' })).toBeVisible();
  }
  await expect(admin.getByRole('button', { name: 'Show your role' })).toBeHidden();

  // A started game is closed to newcomers
  const latecomer = await openGuest(session);
  await latecomer.goto(`/s/${session.id}/games/resistance/join`);
  await expect(latecomer.getByText('This game has already started.')).toBeVisible();
});

test('a name can be used once, and the admin can kick a player before the start', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const admin = await openAdmin(session);
  await admin.goto(`/s/${session.id}/games`);
  await admin.getByRole('button', { name: 'Create' }).click();
  await admin.getByRole('button', { name: 'Open the game' }).click();

  const join = async (name: string) => {
    const guest = await openGuest(session);
    await guest.goto(`/s/${session.id}/games/resistance/join`);
    await guest.getByLabel('Your name').fill(name);
    await guest.getByRole('button', { name: 'Join' }).click();
    return guest;
  };

  const anna = await join('Anna');
  await expect(admin.getByText('1/10 players')).toBeVisible();

  // The same name again is refused and takes no seat
  const impostor = await openGuest(session);
  await impostor.goto(`/s/${session.id}/games/resistance/join`);
  await impostor.getByLabel('Your name').fill('Anna');
  await impostor.getByRole('button', { name: 'Join' }).click();
  await expect(
    impostor.getByText('Somebody already uses that name (or the game is closed, full or has already started).'),
  ).toBeVisible();
  await expect(admin.getByText('1/10 players')).toBeVisible();

  const bartek = await join('Bartek');
  await expect(admin.getByText('2/10 players')).toBeVisible();

  // Kicked from the Players dialog; the seat is gone and the player is told
  await admin.getByRole('button', { name: 'Players' }).click();
  await admin.getByRole('button', { name: 'Kick Anna' }).click();
  await expect(admin.getByRole('button', { name: 'Kick Anna' })).toBeHidden();
  await expect(anna.getByText('You were removed from the game.')).toBeVisible();
  await expect(bartek.getByText('You were removed from the game.')).toBeHidden();
  await admin.getByRole('button', { name: 'Close' }).click();
  await expect(admin.getByText('1/10 players')).toBeVisible();
});
