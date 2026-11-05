import {
  confirmIdentityCard,
  dealPlotCards,
  passWindow,
  pickTeam,
  playImmediateCard,
  playMission,
  playMissionCards,
  playMissionThroughWindows,
  ResistanceGameSetup,
  spyNames,
  startResistanceGame,
  submitTeam,
  takeResponsibility,
  viewOf,
  voteAll,
  waitForDealing,
  waitForPhase,
} from './fixtures/resistance';
import { expect, test } from './fixtures/test';

// Ten players draw three cards a mission. With the seeded deal the first one gives an Open Up, an Opinion Maker and a
// Take Responsibility, the second an Establish Confidence, a Keeping a Close Eye and a No Confidence (see "Plot cards" in
// docs/testing-e2e.md).

/**
 * Plays the first mission so that no card is left in a hand afterwards (a card in a hand opens a window at every step):
 * the Open Up is played, the Take Responsibility takes the Opinion Maker, the Opinion Maker votes first.
 */
async function playFirstMission(setup: ResistanceGameSetup) {
  const { name: leaderName, page: leader, drawn } = await waitForDealing(setup);
  expect([...drawn].sort()).toEqual(['OpenUp', 'OpinionMaker', 'TakeResponsibility']);
  const [openUpName, makerName, takerName, targetName] = [...setup.guests.keys()].filter((name) => name !== leaderName);
  await dealPlotCards(leader, { OpenUp: openUpName, OpinionMaker: makerName, TakeResponsibility: takerName });

  await waitForPhase(setup, 'ResolvingCards');
  await playImmediateCard(setup.guests.get(openUpName)!, 'OpenUp', targetName);
  await confirmIdentityCard(setup.guests.get(targetName)!);
  await waitForPhase(setup, 'SelectingTeam');
  await takeResponsibility(setup.guests.get(takerName)!, makerName);

  await playMission(setup, 'success');
}

test('Establish Confidence, the Confidence window and a peek at a mission card', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  // Ten players and two missions take over 30 seconds on a CI runner (the default timeout)
  test.setTimeout(90_000);
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest }, { plotCards: true, players: 10 });
  await playFirstMission(setup);

  // The Establish Confidence stays with the leader, so only the other two cards are dealt
  const { name: leaderName, page: leader, drawn } = await waitForDealing(setup);
  expect([...drawn].sort()).toEqual(['KeepingCloseEye', 'NoConfidence']);
  const others = [...setup.guests.keys()].filter((name) => name !== leaderName);
  const [peekerName, rejecterName, shownTo] = others;
  await dealPlotCards(leader, { KeepingCloseEye: peekerName, NoConfidence: rejecterName });

  // The leader shows their identity card to somebody, who is the only one to see it
  await waitForPhase(setup, 'ResolvingCards');
  await playImmediateCard(leader, 'EstablishConfidence', shownTo);
  const shown = await confirmIdentityCard(setup.guests.get(shownTo)!);
  expect(shown.name).toBe(leaderName);
  const spies = await spyNames(setup);
  expect(spies.includes(leaderName)).toBe(shown.role === 'Spy');
  await waitForPhase(setup, 'SelectingTeam');

  // A team with one spy that plays Fail
  const resistance = others.filter((name) => !spies.includes(name));
  const view = await viewOf(leader, session);
  const team = [spies[0], ...resistance.slice(0, view.requiredTeamSize! - 1)].slice(0, view.requiredTeamSize!);
  await submitTeam(leader, team);
  await voteAll(setup, 'Approve');

  // Two cards are in the hands, so before the team leaves everybody confirms; only the holder is offered the card
  await waitForPhase(setup, 'ConfidenceWindow');
  for (const [name, page] of setup.guests) {
    const reject = page.getByRole('button', { name: 'Reject the approved team' });
    await expect(reject)[name === rejecterName ? 'toBeVisible' : 'toBeHidden']();
  }
  await passWindow(setup);
  await waitForPhase(setup, 'PlayingMission');
  await playMissionCards(setup, team, [spies[0]]);

  // The result is public at once, but the mission cards stay hidden until somebody looks at one
  const afterMission = await waitForPhase(setup, 'PeekWindow');
  expect(afterMission.missionResults).toHaveLength(2);
  const peeker = setup.guests.get(peekerName)!;
  const eye = peeker.getByRole('region', { name: 'Keeping a Close Eye on You' });
  const target =
    team.find((name) => name !== peekerName && name !== spies[0]) ?? team.find((name) => name !== peekerName)!;
  await eye.getByLabel('Whose mission card are you looking at').selectOption({ label: target });
  await eye.getByRole('button', { name: 'Look at the card' }).click();
  await expect(eye.getByText(`${target} played: ${spies.includes(target) ? 'Fail' : 'Success'}`)).toBeVisible();

  // Everybody else only learns who looked at whom
  const watcher = setup.guests.get(rejecterName)!;
  await expect(
    watcher
      .getByRole('region', { name: 'Plot cards played' })
      .getByText(`${peekerName} played “Keeping a Close Eye on You” and looks at the mission card of ${target}`),
  ).toBeVisible();
  await expect(watcher.getByText(/ played: (Success|Fail)/)).toBeHidden();

  await passWindow(setup);
  await waitForPhase(setup, 'MissionResult');
});

test('In the Spotlight forces a team member to play their mission card openly', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  // The In the Spotlight is only drawn on the fourth mission, and a card in a hand opens a window at every step before
  test.setTimeout(150_000);
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest }, { plotCards: true, players: 10 });
  await playFirstMission(setup);

  // The second mission: the Establish Confidence is played and the other two cards stay in the hands (nothing is played)
  const second = await waitForDealing(setup);
  expect([...second.drawn].sort()).toEqual(['KeepingCloseEye', 'NoConfidence']);
  const secondOthers = [...setup.guests.keys()].filter((name) => name !== second.name);
  await dealPlotCards(second.page, { KeepingCloseEye: secondOthers[0], NoConfidence: secondOthers[1] });
  await waitForPhase(setup, 'ResolvingCards');
  await playImmediateCard(second.page, 'EstablishConfidence', secondOthers[2]);
  await confirmIdentityCard(setup.guests.get(secondOthers[2])!);
  await playMissionThroughWindows(setup, 'success');

  // The third one: a Strong Leader, a Keeping a Close Eye and a No Confidence more; the score stays open
  await waitForPhase(setup, 'LeaderWindow');
  await passWindow(setup);
  const third = await waitForDealing(setup);
  expect([...third.drawn].sort()).toEqual(['KeepingCloseEye', 'NoConfidence', 'StrongLeader']);
  const thirdOthers = [...setup.guests.keys()].filter((name) => name !== third.name);
  await dealPlotCards(third.page, {
    KeepingCloseEye: thirdOthers[0],
    NoConfidence: thirdOthers[1],
    StrongLeader: thirdOthers[2],
  });
  await playMissionThroughWindows(setup, 'fail');

  // The fourth one: two Overheard Conversations are played at once and the In the Spotlight waits for the Confidence window
  await waitForPhase(setup, 'LeaderWindow');
  await passWindow(setup);
  const fourth = await waitForDealing(setup);
  expect([...fourth.drawn].sort()).toEqual(['InTheSpotlight', 'OverheardConversation', 'OverheardConversation']);
  const [spotlightName, firstListener, secondListener] = [...setup.guests.keys()].filter(
    (name) => name !== fourth.name,
  );
  await dealPlotCards(fourth.page, {
    InTheSpotlight: spotlightName,
    OverheardConversation: [firstListener, secondListener],
  });
  const dealt = await waitForPhase(setup, 'ResolvingCards');
  const seats = dealt.players.map((player) => player.name);
  for (const listener of [firstListener, secondListener]) {
    const next = seats[(seats.indexOf(listener) + 1) % seats.length];
    await playImmediateCard(setup.guests.get(listener)!, 'OverheardConversation', next);
    await confirmIdentityCard(setup.guests.get(listener)!);
  }

  const { leader, team } = await pickTeam(setup, 'success');
  await submitTeam(leader, team);
  await voteAll(setup, 'Approve');
  await waitForPhase(setup, 'ConfidenceWindow');

  // The holder names a team member (not themselves); it is public who has to play openly, and it is no secret card
  const forced = team.find((name) => name !== spotlightName)!;
  const spotlight = setup.guests.get(spotlightName)!.getByRole('region', { name: 'In the Spotlight' });
  await spotlight.getByLabel('Who do you force to play an open mission card').selectOption({ label: forced });
  await spotlight.getByRole('button', { name: 'Play the card' }).click();
  const mustPlayOpen = `${forced} must play their mission card openly`;
  for (const page of setup.guests.values()) {
    await expect(page.getByRole('status').filter({ hasText: mustPlayOpen })).toBeVisible();
  }

  await passWindow(setup);
  await waitForPhase(setup, 'PlayingMission');
  await playMissionCards(setup, [forced], []);
  for (const page of setup.guests.values()) {
    await expect(
      page.getByRole('status').filter({ hasText: `${forced} played their card openly: Success.` }),
    ).toBeVisible();
  }
});
