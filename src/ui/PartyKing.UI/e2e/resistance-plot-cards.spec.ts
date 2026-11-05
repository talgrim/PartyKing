import {
  dealPlotCards,
  nameOf,
  playMission,
  spyNames,
  startResistanceGame,
  submitTeam,
  viewOf,
  viewOfPlayer,
  vote,
  voteAll,
  waitForDealing,
  waitForPhase,
} from './fixtures/resistance';
import { expect, test } from './fixtures/test';

// The e2e API is seeded, so five players always draw Open Up first and Strong Leader on the second mission, and seven
// players draw an Opinion Maker and No Confidence on the first one (see "Plot cards" in docs/testing-e2e.md).

test('the leader deals the drawn card, the holder plays an immediate card and the team is picked after that', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest }, { plotCards: true });

  const { name: leaderName, page: leader, drawn } = await waitForDealing(setup);
  expect(drawn).toEqual(['OpenUp']);

  // Nobody picks a team before the cards are dealt, and only the leader deals
  await expect(leader.getByRole('region', { name: 'Deal the Plot cards' })).toBeVisible();
  await expect(leader.getByRole('group', { name: 'Pick the team' })).toBeHidden();
  const others = [...setup.guests.keys()].filter((name) => name !== leaderName);
  for (const name of others) {
    await expect(setup.guests.get(name)!.getByRole('region', { name: 'Deal the Plot cards' })).toBeHidden();
  }

  const [holderName, targetName] = others;
  await dealPlotCards(leader, { OpenUp: holderName });
  await waitForPhase(setup, 'ResolvingCards');
  expect((await viewOfPlayer(setup, holderName)).yourPlotCards.map((card) => card.type)).toEqual(['OpenUp']);

  // The holder plays it on another player, who gets to see an identity card and confirms that
  const holder = setup.guests.get(holderName)!;
  const played = holder.getByRole('region', { name: 'Immediate cards' });
  await played.getByLabel(/Who do you play the .Open Up. card on/).selectOption({ label: targetName });
  await played.getByRole('button', { name: 'Play the card' }).click();

  const target = setup.guests.get(targetName)!;
  const reveal = target.getByRole('region', { name: 'Immediate cards' });
  const identity = reveal.getByText(/Identity card of .+: (Spy|Resistance)\./);
  await expect(identity).toBeVisible();
  const [, shownName, shownRole] = (await identity.innerText()).match(/Identity card of (.+): (Spy|Resistance)\./)!;
  expect((await spyNames(setup)).includes(shownName)).toBe(shownRole === 'Spy');

  await expect.poll(async () => (await viewOf(leader, session)).phase).toBe('ResolvingCards');
  await reveal.getByRole('button', { name: "I've seen it" }).click();
  await waitForPhase(setup, 'SelectingTeam');
  await expect(leader.getByRole('group', { name: 'Pick the team' })).toBeVisible();
});

test('a Strong Leader takes the lead in the window that follows a rejected team', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest }, { plotCards: true });

  // The first mission: Open Up is dealt, played and confirmed
  const first = await waitForDealing(setup);
  const [firstHolder, firstTarget] = [...setup.guests.keys()].filter((name) => name !== first.name);
  await dealPlotCards(first.page, { OpenUp: firstHolder });
  const played = setup.guests.get(firstHolder)!.getByRole('region', { name: 'Immediate cards' });
  await played.getByLabel(/Who do you play the .Open Up. card on/).selectOption({ label: firstTarget });
  await played.getByRole('button', { name: 'Play the card' }).click();
  await setup.guests.get(firstTarget)!.getByRole('button', { name: "I've seen it" }).click();
  await playMission(setup, 'success');

  // The second one draws a Strong Leader, which the leader gives to somebody else
  const { name: leaderName, page: leader, drawn } = await waitForDealing(setup);
  expect(drawn).toEqual(['StrongLeader']);
  const others = [...setup.guests.keys()].filter((name) => name !== leaderName);
  const holderName = others[0];
  await dealPlotCards(leader, { StrongLeader: holderName });
  const view = await waitForPhase(setup, 'SelectingTeam');

  // A rejected team does not pass the lead on yet: the players first decide whether to play a card
  await submitTeam(leader, others.slice(0, view.requiredTeamSize!));
  await voteAll(setup, 'Reject');
  await waitForPhase(setup, 'LeaderWindow');

  // Only the holder is offered the card, and nobody can tell who that is from the window
  const takeLead = (name: string) => setup.guests.get(name)!.getByRole('button', { name: 'Take the lead' });
  await expect(takeLead(holderName)).toBeVisible();
  for (const name of setup.guests.keys()) {
    if (name !== holderName) {
      await expect(takeLead(name)).toBeHidden();
      await expect(setup.guests.get(name)!.getByRole('button', { name: "I'm not playing a card" })).toBeVisible();
    }
  }

  await takeLead(holderName).click();
  const after = await waitForPhase(setup, 'SelectingTeam');
  expect(nameOf(after, after.leaderPlayerId!)).toBe(holderName);
  expect(after.rejectedTeams).toBe(1);
  expect(after.missionNumber).toBe(view.missionNumber);
  await expect(
    setup.guests.get(holderName)!.getByRole('region', { name: 'Plot cards played' }).getByText(/Strong Leader/),
  ).toBeVisible();
});

test('an Opinion Maker votes first and a No Confidence card rejects an approved team', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest }, { plotCards: true, players: 7 });

  const { name: leaderName, page: leader, drawn } = await waitForDealing(setup);
  expect([...drawn].sort()).toEqual(['NoConfidence', 'OpinionMaker']);
  const others = [...setup.guests.keys()].filter((name) => name !== leaderName);
  const [makerName, holderName] = others;
  await dealPlotCards(leader, { OpinionMaker: makerName, NoConfidence: holderName });
  const picking = await waitForPhase(setup, 'SelectingTeam');
  expect(picking.opinionMakerPlayerIds).toEqual([picking.players.find((player) => player.name === makerName)!.id]);

  await submitTeam(leader, others.slice(0, picking.requiredTeamSize!));
  await waitForPhase(setup, 'Voting');
  const voter = setup.guests.get(others[2])!;
  await expect(
    voter.getByRole('status').filter({ hasText: `The Opinion Maker votes first: ${makerName}.` }),
  ).toBeVisible();

  // The Opinion Maker's vote is the first one, everybody else follows
  await vote(setup.guests.get(makerName)!, 'Approve');
  for (const [name, page] of setup.guests) {
    if (name !== makerName) {
      await vote(page, 'Approve');
    }
  }

  // The team is approved, but before it leaves everybody confirms that they play no card (No Confidence is one)
  await waitForPhase(setup, 'ConfidenceWindow');
  for (const name of setup.guests.keys()) {
    const reject = setup.guests.get(name)!.getByRole('button', { name: 'Reject the approved team' });
    await expect(reject)[name === holderName ? 'toBeVisible' : 'toBeHidden']();
  }
  await setup.guests.get(holderName)!.getByRole('button', { name: 'Reject the approved team' }).click();

  // It counts like a rejected team: the lead moves on and the mission is still the first one
  const after = await waitForPhase(setup, 'SelectingTeam');
  expect(after.leaderPlayerId).not.toBe(picking.leaderPlayerId);
  expect(after.rejectedTeams).toBe(1);
  expect(after.missionNumber).toBe(picking.missionNumber);
  const newLeader = setup.guests.get(nameOf(after, after.leaderPlayerId!))!;
  await expect(newLeader.getByRole('status').filter({ hasText: 'Rejected teams in a row: 1/5' })).toBeVisible();
});
