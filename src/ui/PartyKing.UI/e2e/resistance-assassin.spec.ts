import {
  PLAYER_NAMES,
  playMission,
  spyNames,
  startResistanceGame,
  viewOfPlayer,
  waitForPhase,
} from './fixtures/resistance';
import type { ResistanceGameSetup } from './fixtures/resistance';
import { expect, test } from './fixtures/test';

/** Everybody's character, read from their own view: the Commander, the Assassin and the others by name. */
async function charactersOf(setup: ResistanceGameSetup) {
  const names = PLAYER_NAMES.slice(0, setup.guests.size);
  const views = await Promise.all(names.map(async (name) => ({ name, view: await viewOfPlayer(setup, name) })));
  const commander = views.find((x) => x.view.yourCharacter === 'Commander')!;
  const assassin = views.find((x) => x.view.yourCharacter === 'Assassin')!;

  return { names, views, commander, assassin };
}

/** Starts a game with the module and plays three successful missions, which leaves the game waiting for the Assassin. */
async function toTheAssassination(setup: ResistanceGameSetup) {
  await playMission(setup, 'success');
  await playMission(setup, 'success');
  await playMission(setup, 'success');

  return waitForPhase(setup, 'Assassination');
}

test('the Assassin module deals a Commander who knows the spies and an Assassin, and the third success waits for the Assassin', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest }, { assassin: true });
  const spies = await spyNames(setup);
  const { names, views, commander, assassin } = await charactersOf(setup);

  // One Commander on the Resistance side, who knows both spies, and one Assassin among the spies
  expect(views.filter((x) => x.view.yourCharacter === 'Commander')).toHaveLength(1);
  expect(views.filter((x) => x.view.yourCharacter === 'Assassin')).toHaveLength(1);
  expect(commander.view.yourRole).toBe('Resistance');
  expect(assassin.view.yourRole).toBe('Spy');
  const idOf = (name: string) => views.find((x) => x.name === name)!.view.yourPlayerId;
  expect([...commander.view.fellowSpies!].sort()).toEqual(spies.map(idOf).sort());
  const otherSpy = spies.find((name) => name !== assassin.name)!;
  expect(assassin.view.fellowSpies).toEqual([idOf(otherSpy)]);

  // Nobody else learns who the Commander is, and the cards say so only to their owners
  for (const { name, view } of views) {
    if (name !== commander.name) {
      expect(view.yourCharacter === 'Commander').toBe(false);
    }
    if (view.yourRole === 'Resistance' && name !== commander.name) {
      expect(view.fellowSpies).toBeNull();
    }
    const page = setup.guests.get(name)!;
    await page.getByRole('button', { name: 'Show your role' }).click();
    await (name === commander.name
      ? expect(page.getByText(/You are the Commander/)).toBeVisible()
      : expect(page.getByText(/You are the Commander/)).toBeHidden());
    await (name === assassin.name
      ? expect(page.getByText(/You are the Assassin/)).toBeVisible()
      : expect(page.getByText(/You are the Assassin/)).toBeHidden());
  }
  expect(names).toHaveLength(5);

  // Three successes do not end the game: it waits for the Assassin, with no winner and no roles revealed
  const waiting = await toTheAssassination(setup);
  expect(waiting.winner).toBeNull();
  expect(waiting.revealedRoles).toBeNull();
  for (const name of names) {
    const view = await viewOfPlayer(setup, name);
    expect(view.phase).toBe('Assassination');
    expect(view.winner).toBeNull();
    expect(view.revealedRoles).toBeNull();
    expect(view.assassinationTargetPlayerId).toBeNull();
    const page = setup.guests.get(name)!;
    await (name === assassin.name
      ? expect(page.getByLabel('Player to name')).toBeVisible()
      : expect(page.getByLabel('Player to name')).toBeHidden());
    await expect(page.getByText(/the Assassin is choosing the player they take for the Commander/)).toBeVisible();
  }
});

test('the spies win when the Assassin names the Commander', async ({ createSession, openAdmin, openGuest }) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest }, { assassin: true });
  const { names, commander, assassin } = await charactersOf(setup);
  await toTheAssassination(setup);

  const page = setup.guests.get(assassin.name)!;
  await page.getByLabel('Player to name').selectOption({ label: commander.name });
  await page.getByRole('button', { name: 'Name the Commander' }).click();

  const over = await waitForPhase(setup, 'GameOver');
  expect(over.winner).toBe('Spy');
  expect(over.assassinationTargetPlayerId).toBe(commander.view.yourPlayerId);
  expect(over.revealedRoles).toHaveLength(names.length);
  expect(over.revealedRoles!.find((x) => x.playerId === commander.view.yourPlayerId)!.character).toBe('Commander');
  await expect(page.getByText(new RegExp(`the Assassin named ${commander.name}, who is the Commander`))).toBeVisible();
});

test('the resistance wins when the Assassin names anybody else, and the host can restart from there', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest }, { assassin: true });
  const { names, commander, assassin } = await charactersOf(setup);
  await toTheAssassination(setup);
  const wrong = names.find((name) => name !== commander.name && name !== assassin.name)!;

  const page = setup.guests.get(assassin.name)!;
  await page.getByLabel('Player to name').selectOption({ label: wrong });
  await page.getByRole('button', { name: 'Name the Commander' }).click();

  const over = await waitForPhase(setup, 'GameOver');
  expect(over.winner).toBe('Resistance');
  await expect(page.getByText(new RegExp(`the Assassin named ${wrong}, who is not the Commander`))).toBeVisible();

  // A finished game restarts as ever
  await setup.admin.getByRole('button', { name: 'Restart game' }).click();
  await expect(setup.admin.getByRole('button', { name: 'Start game' })).toBeVisible();
});

test('the host can restart a game that waits for an absent Assassin', async ({ createSession, openAdmin, openGuest }) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest }, { assassin: true });
  await toTheAssassination(setup);

  await setup.admin.getByRole('button', { name: 'Restart game' }).click();

  await expect(setup.admin.getByRole('button', { name: 'Start game' })).toBeVisible();
});
