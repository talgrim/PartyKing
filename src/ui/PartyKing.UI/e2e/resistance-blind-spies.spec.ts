import { PLAYER_NAMES, spyNames, startResistanceGame, viewOfPlayer } from './fixtures/resistance';
import { expect, test } from './fixtures/test';

test('with Blind Spies no spy is told who the other spies are', async ({ createSession, openAdmin, openGuest }) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest }, { blindSpies: true });
  const spies = await spyNames(setup);
  expect(spies).toHaveLength(2);

  for (const name of PLAYER_NAMES.slice(0, setup.guests.size)) {
    const page = setup.guests.get(name)!;
    const view = await viewOfPlayer(setup, name);
    await page.getByRole('button', { name: 'Show your role' }).click();
    await expect(page.getByRole('button', { name: 'Hide your role' })).toBeVisible();

    // Nobody gets a name on the wire or on the card; a spy only learns that they know no one
    await expect(page.getByRole('list', { name: 'Fellow spies' })).toBeHidden();
    if (spies.includes(name)) {
      expect(view.fellowSpies).toEqual([]);
      await expect(page.getByText('Blind Spies: you do not know who the other spies are.')).toBeVisible();
    } else {
      expect(view.fellowSpies).toBeNull();
      await expect(page.getByText('Blind Spies: you do not know who the other spies are.')).toBeHidden();
    }
  }
});
