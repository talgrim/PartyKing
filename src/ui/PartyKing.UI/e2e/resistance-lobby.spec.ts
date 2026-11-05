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
