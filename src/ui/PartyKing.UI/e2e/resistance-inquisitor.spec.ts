import {
  confirmInquisition,
  continueMission,
  examineOptions,
  examinePlayer,
  nameOf,
  playMission,
  playMissionToInquisition,
  startResistanceGame,
  viewOf,
  viewOfPlayer,
  waitForPhase,
} from './fixtures/resistance';
import { expect, test } from './fixtures/test';

test('the Inquisitor examines after missions 2 and 3, the token passes on and nobody else sees the result', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest }, { inquisitor: true });
  const names = [...setup.guests.keys()];

  // The token is public from the start, with one player
  const start = await viewOfPlayer(setup, names[0]);
  expect(start.inquisitorPlayerId).not.toBeNull();
  expect(start.pastInquisitorPlayerIds).toEqual([start.inquisitorPlayerId]);

  // Mission 1 has no Inquisition: it goes straight to "Next"
  await playMission(setup, 'success');

  // Mission 2 (failed, so that nobody has won by the third) ends in the Inquisition
  const firstLeader = await playMissionToInquisition(setup, 'fail');
  const first = await viewOf(firstLeader, session);
  const firstHolderName = nameOf(first, first.inquisitorPlayerId!);
  const firstHolder = setup.guests.get(firstHolderName)!;
  await expect(firstHolder.getByRole('region', { name: 'Inquisitor' })).toBeVisible();
  expect(await examineOptions(firstHolder)).toEqual(names.filter((name) => name !== firstHolderName));

  const target = names.find((name) => name !== firstHolderName)!;
  const seen = await examinePlayer(firstHolder, target);
  expect(seen.name).toBe(target);
  // What the holder was shown is the target's real loyalty
  expect(seen.role).toBe((await viewOfPlayer(setup, target)).yourRole);

  // The token already passed to the target (public); the result is the examiner's alone
  const during = await waitForPhase(setup, 'Inquisition');
  expect(nameOf(during, during.inquisitorPlayerId!)).toBe(target);
  expect(nameOf(during, during.inquisitionExaminerPlayerId!)).toBe(firstHolderName);
  expect(nameOf(during, during.inquisitionExaminedPlayerId!)).toBe(target);
  for (const name of names) {
    const view = await viewOfPlayer(setup, name);
    expect(view.yourInquisition === null).toBe(name !== firstHolderName);
  }
  expect((await viewOf(setup.admin, session)).yourInquisition).toBeNull();
  await expect(setup.guests.get(target)!.getByRole('region', { name: 'Inquisitor' })).toBeHidden();

  // The game does not move on until the examiner confirms, and then the result is gone
  await confirmInquisition(firstHolder);
  await waitForPhase(setup, 'MissionResult');
  expect((await viewOfPlayer(setup, firstHolderName)).yourInquisition).toBeNull();
  await continueMission(firstLeader);

  // Mission 3: the new holder can pick neither earlier holder
  const secondLeader = await playMissionToInquisition(setup, 'success');
  const second = await viewOf(secondLeader, session);
  expect(nameOf(second, second.inquisitorPlayerId!)).toBe(target);
  expect(second.pastInquisitorPlayerIds).toHaveLength(2);
  const secondHolder = setup.guests.get(target)!;
  const rest = names.filter((name) => name !== firstHolderName && name !== target);
  expect(await examineOptions(secondHolder)).toEqual(rest);
});

test('without the module there is no token and no Inquisition', async ({ createSession, openAdmin, openGuest }) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest });

  const view = await viewOfPlayer(setup, [...setup.guests.keys()][0]);
  expect(view.inquisitorPlayerId).toBeNull();
  await playMission(setup, 'success');
  await playMission(setup, 'fail');
  expect((await waitForPhase(setup, 'SelectingTeam')).missionNumber).toBe(3);
});
