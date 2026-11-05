import {
  PLAYER_NAMES,
  playMission,
  spyNames,
  startResistanceGame,
  viewOfPlayer,
  waitForLeader,
} from './fixtures/resistance';
import { expect, test } from './fixtures/test';

test('the Defector module deals one Defector to each side and flips a public switch card at mission 3', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest }, { defector: true });
  const names = PLAYER_NAMES.slice(0, setup.guests.size);
  const spies = await spyNames(setup);

  // Everybody knows only their own character, and the note is on the Defectors' cards only
  const defectors: string[] = [];
  for (const name of names) {
    const view = await viewOfPlayer(setup, name);
    expect(['Standard', 'Defector']).toContain(view.yourCharacter);
    expect(view.defectorFlips).toEqual([]);
    if (view.yourCharacter === 'Defector') {
      defectors.push(name);
    }

    const page = setup.guests.get(name)!;
    await page.getByRole('button', { name: 'Show your role' }).click();
    const note = page.getByText(/You are a Defector/);
    await (view.yourCharacter === 'Defector' ? expect(note).toBeVisible() : expect(note).toBeHidden());
  }
  expect(defectors).toHaveLength(2);
  expect(defectors.filter((name) => spies.includes(name))).toHaveLength(1);
  const spyDefector = defectors.find((name) => spies.includes(name))!;
  const resistanceDefector = defectors.find((name) => !spies.includes(name))!;
  const otherSpy = spies.find((name) => name !== spyDefector)!;

  // The spy Defector keeps their eyes closed in the reveal: they know no spy, but the other spy knows them
  const spyDefectorId = (await viewOfPlayer(setup, spyDefector)).yourPlayerId;
  expect((await viewOfPlayer(setup, spyDefector)).fellowSpies).toEqual([]);
  expect((await viewOfPlayer(setup, otherSpy)).fellowSpies).toEqual([spyDefectorId]);
  expect((await viewOfPlayer(setup, resistanceDefector)).fellowSpies).toBeNull();
  await expect(setup.admin.getByRole('list', { name: 'Flipped switch cards' })).toBeHidden();

  // Nothing is flipped before mission 3
  await playMission(setup, 'success');
  await playMission(setup, 'success');
  await waitForLeader(setup);

  const flips = (await viewOfPlayer(setup, otherSpy)).defectorFlips;
  expect(flips).toHaveLength(1);
  for (const name of names) {
    const view = await viewOfPlayer(setup, name);
    expect(view.defectorFlips).toEqual(flips);
    expect(view.missionNumber).toBe(3);
  }
  const card = flips[0] ? 'Mission 3: Change Allegiance' : 'Mission 3: No Change';
  await expect(setup.admin.getByRole('list', { name: 'Flipped switch cards' }).getByText(card)).toBeVisible();

  // On Change Allegiance both Defectors are on the other side, and the spies no longer know the spy Defector
  const switched = flips[0];
  expect((await viewOfPlayer(setup, spyDefector)).yourRole).toBe(switched ? 'Resistance' : 'Spy');
  expect((await viewOfPlayer(setup, resistanceDefector)).yourRole).toBe(switched ? 'Spy' : 'Resistance');
  expect((await viewOfPlayer(setup, otherSpy)).fellowSpies).toEqual(switched ? [] : [spyDefectorId]);
  expect((await viewOfPlayer(setup, resistanceDefector)).fellowSpies).toEqual(switched ? [] : null);
  expect((await viewOfPlayer(setup, spyDefector)).fellowSpies).toEqual(switched ? null : []);
});
