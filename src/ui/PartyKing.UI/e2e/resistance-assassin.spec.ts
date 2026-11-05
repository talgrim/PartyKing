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

test('the optional characters hide and show the right people, with seven players', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const setup = await startResistanceGame(
    session,
    { openAdmin, openGuest },
    { players: 7, assassinCharacters: ['Body Guard', 'Deep Cover', 'Blind Spy'] },
  );
  const names = PLAYER_NAMES.slice(0, 7);
  const views = await Promise.all(names.map(async (name) => ({ name, view: await viewOfPlayer(setup, name) })));
  const withCharacter = (character: string) => {
    const matches = views.filter((x) => x.view.yourCharacter === character);
    expect(matches).toHaveLength(1);

    return matches[0];
  };
  const commander = withCharacter('Commander');
  const assassin = withCharacter('Assassin');
  const bodyGuard = withCharacter('BodyGuard');
  const deepCover = withCharacter('DeepCover');
  const blindSpy = withCharacter('BlindSpy');
  expect(views.filter((x) => x.view.yourCharacter === 'FalseCommander')).toHaveLength(0);
  const id = (x: { view: { yourPlayerId: string | null } }) => x.view.yourPlayerId!;

  // The Commander sees the Assassin and the Blind Spy, but not Deep Cover
  expect([...commander.view.fellowSpies!].sort()).toEqual([id(assassin), id(blindSpy)].sort());
  // The Blind Spy knows no spy and is known to none; Deep Cover and the Assassin know each other
  expect(blindSpy.view.fellowSpies).toEqual([]);
  expect(assassin.view.fellowSpies).toEqual([id(deepCover)]);
  expect(deepCover.view.fellowSpies).toEqual([id(assassin)]);
  // The Body Guard knows the Commander, and nobody else gets a candidate list or a list of spies
  expect(bodyGuard.view.yourCommanderCandidates).toEqual([id(commander)]);
  expect(bodyGuard.view.fellowSpies).toBeNull();
  for (const x of views.filter((y) => y.name !== bodyGuard.name)) {
    expect(x.view.yourCommanderCandidates).toBeNull();
  }

  // The cards tell each of them who they are, and only them
  const notes: [string, RegExp][] = [
    [bodyGuard.name, /You are the Body Guard/],
    [deepCover.name, /You are Deep Cover/],
    [blindSpy.name, /You are the Blind Spy/],
  ];
  for (const [name, note] of notes) {
    const page = setup.guests.get(name)!;
    await page.getByRole('button', { name: 'Show your role' }).click();
    await expect(page.getByText(note)).toBeVisible();
  }
  const guardPage = setup.guests.get(bodyGuard.name)!;
  await expect(guardPage.getByRole('list', { name: 'Possible Commanders' })).toHaveText(commander.name);
  const plainPage = setup.guests.get(names.find((name) => ![commander, assassin, bodyGuard, deepCover, blindSpy].some((x) => x.name === name))!)!;
  await plainPage.getByRole('button', { name: 'Show your role' }).click();
  await expect(plainPage.getByText(/You are (the|Deep)/)).toBeHidden();
});

test('a Body Guard and a False Commander at five players look alike to the Body Guard', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const setup = await startResistanceGame(
    session,
    { openAdmin, openGuest },
    { assassinCharacters: ['Body Guard', 'False Commander'] },
  );
  const names = PLAYER_NAMES.slice(0, 5);
  const views = await Promise.all(names.map(async (name) => ({ name, view: await viewOfPlayer(setup, name) })));
  const commander = views.find((x) => x.view.yourCharacter === 'Commander')!;
  const falseCommander = views.find((x) => x.view.yourCharacter === 'FalseCommander')!;
  const bodyGuard = views.find((x) => x.view.yourCharacter === 'BodyGuard')!;

  // Two candidates, in seat order, one of whom is the Commander and one a spy
  expect(bodyGuard.view.yourCommanderCandidates).toHaveLength(2);
  expect([...bodyGuard.view.yourCommanderCandidates!].sort()).toEqual(
    [commander.view.yourPlayerId!, falseCommander.view.yourPlayerId!].sort(),
  );
  expect(falseCommander.view.yourRole).toBe('Spy');
  // The Commander sees both spies, the False Commander among them
  expect(commander.view.fellowSpies).toContain(falseCommander.view.yourPlayerId);
  expect(commander.view.fellowSpies).toHaveLength(2);

  const page = setup.guests.get(bodyGuard.name)!;
  await page.getByRole('button', { name: 'Show your role' }).click();
  await expect(page.getByRole('list', { name: 'Possible Commanders' }).getByRole('listitem')).toHaveCount(2);
});
