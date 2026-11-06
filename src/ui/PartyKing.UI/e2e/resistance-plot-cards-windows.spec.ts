import {
  confirmIdentityCard,
  dealPlotCards,
  passWindow,
  pickTeam,
  playImmediateCard,
  playMission,
  playMissionCards,
  spyNames,
  startResistanceGame,
  submitTeam,
  viewOf,
  voteAll,
  waitForDealing,
  waitForPhase,
} from './fixtures/resistance';
import { expect, test } from './fixtures/test';

// With the seeded deal, ten players draw an Establish Confidence, a Keeping a Close Eye and a No Confidence on the first
// mission, six players an Overheard Conversation on the first and an In the Spotlight on the second (see "Plot cards" in
// docs/testing-e2e.md).

test('Establish Confidence, the Confidence window and a peek at a mission card', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest }, { plotCards: true, players: 10 });

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
  expect(afterMission.missionResults).toHaveLength(1);
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
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest }, { plotCards: true, players: 6 });

  // The first mission: the Overheard Conversation is played at once, so no card stays in a hand and no window opens
  const first = await waitForDealing(setup);
  expect(first.drawn).toEqual(['OverheardConversation']);
  const listenerName = [...setup.guests.keys()].find((name) => name !== first.name)!;
  await dealPlotCards(first.page, { OverheardConversation: listenerName });
  const dealt = await waitForPhase(setup, 'ResolvingCards');
  const seats = dealt.players.map((player) => player.name);
  const next = seats[(seats.indexOf(listenerName) + 1) % seats.length];
  await playImmediateCard(setup.guests.get(listenerName)!, 'OverheardConversation', next);
  await confirmIdentityCard(setup.guests.get(listenerName)!);
  await playMission(setup, 'success');

  // The second mission draws the In the Spotlight, which waits in a hand for the Confidence window
  const second = await waitForDealing(setup);
  expect(second.drawn).toEqual(['InTheSpotlight']);
  const spotlightName = [...setup.guests.keys()].find((name) => name !== second.name)!;
  await dealPlotCards(second.page, { InTheSpotlight: spotlightName });
  await waitForPhase(setup, 'SelectingTeam');

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
