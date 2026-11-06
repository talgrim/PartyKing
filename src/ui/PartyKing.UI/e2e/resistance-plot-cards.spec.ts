import {
  confirmIdentityCard,
  dealPlotCards,
  nameOf,
  passWindow,
  playImmediateCard,
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

// The e2e API is seeded, so five players always draw Open Up first and Strong Leader on the second mission, seven
// players an Opinion Maker and No Confidence on the first one, nine an Opinion Maker, a Keeping a Close Eye and a Take
// Responsibility, six an Overheard Conversation (see "Plot cards" in docs/testing-e2e.md).

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

  // A rejected team moves the lead to the next seat, and whoever already leads has no use for the card
  const seats = (await viewOf(leader, session)).players.map((player) => player.name);
  const nextLeaderName = seats[(seats.indexOf(leaderName) + 1) % seats.length];
  const holderName = others.find((name) => name !== nextLeaderName)!;
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
    setup.guests
      .get(holderName)!
      .getByRole('region', { name: 'Plot cards played' })
      .getByText(/Strong Leader/),
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

test('Take Responsibility moves the visible Opinion Maker to the player who plays it', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest }, { plotCards: true, players: 9 });

  // Nine players draw three cards on the first mission; none of them is played at once, so the team is picked right away
  const { name: leaderName, page: leader, drawn } = await waitForDealing(setup);
  expect([...drawn].sort()).toEqual(['KeepingCloseEye', 'OpinionMaker', 'TakeResponsibility']);
  const [eyeName, makerName, takerName] = [...setup.guests.keys()].filter((name) => name !== leaderName);
  await dealPlotCards(leader, { KeepingCloseEye: eyeName, OpinionMaker: makerName, TakeResponsibility: takerName });

  const before = await waitForPhase(setup, 'SelectingTeam');
  const idOf = (name: string) => before.players.find((player) => player.name === name)!.id;
  expect(before.opinionMakerPlayerIds).toEqual([idOf(makerName)]);

  // The holder takes the visible card; the Opinion Maker is the only thing the target has, so the server takes it
  const taker = setup.guests.get(takerName)!;
  const section = taker.getByRole('region', { name: 'Take Responsibility', exact: true });
  await expect(section).toBeVisible();
  await section.getByLabel('Which player do you take a card from').selectOption({ label: makerName });
  await expect(section.getByRole('radio')).toHaveCount(0);
  await section.getByRole('button', { name: 'Take the card' }).click();
  await expect(section).toBeHidden();

  const after = await viewOf(leader, session);
  expect(after.opinionMakerPlayerIds).toEqual([idOf(takerName)]);
  expect(after.playedPlotCards.map((card) => [card.type, card.holderPlayerId, card.targetPlayerId])).toContainEqual([
    'TakeResponsibility',
    idOf(takerName),
    idOf(makerName),
  ]);
  expect((await viewOfPlayer(setup, takerName)).yourPlotCards).toEqual([
    expect.objectContaining({ type: 'OpinionMaker', inPlay: true }),
  ]);
  expect((await viewOfPlayer(setup, makerName)).yourPlotCards).toEqual([]);
  await expect(
    leader
      .getByRole('region', { name: 'Plot cards played' })
      .getByText(`${takerName} played “Take Responsibility” and takes a Plot card from ${makerName}`),
  ).toBeVisible();

  // The card has moved for good: the new owner now votes first
  const others = [...setup.guests.keys()].filter((name) => name !== leaderName);
  await submitTeam(leader, others.slice(0, after.requiredTeamSize!));
  await waitForPhase(setup, 'Voting');
  await expect(
    setup.guests
      .get(makerName)!
      .getByRole('status')
      .filter({ hasText: `The Opinion Maker votes first: ${takerName}.` }),
  ).toBeVisible();
  await vote(taker, 'Approve');
  for (const [name, page] of setup.guests) {
    if (name !== takerName) {
      await vote(page, 'Approve');
    }
  }

  // The Keeping a Close Eye is still in a hand, so the approved team waits for the Confidence window
  await waitForPhase(setup, 'ConfidenceWindow');
  await passWindow(setup);
  await waitForPhase(setup, 'PlayingMission');
});

test('Overheard Conversation shows the identity card of a neighbour to its holder only', async ({
  createSession,
  openAdmin,
  openGuest,
}) => {
  const session = await createSession();
  const setup = await startResistanceGame(session, { openAdmin, openGuest }, { plotCards: true, players: 6 });

  // Six players draw one card on the first mission
  const { name: leaderName, page: leader, drawn } = await waitForDealing(setup);
  expect(drawn).toEqual(['OverheardConversation']);
  const [listenerName, bystanderName] = [...setup.guests.keys()].filter((name) => name !== leaderName);
  await dealPlotCards(leader, { OverheardConversation: listenerName });
  const dealt = await waitForPhase(setup, 'ResolvingCards');

  // Only the two players next to the holder at the table can be listened in on
  const seats = dealt.players.map((player) => player.name);
  const at = seats.indexOf(listenerName);
  const neighbours = [seats[(at + seats.length - 1) % seats.length], seats[(at + 1) % seats.length]];
  const listener = setup.guests.get(listenerName)!;
  const picker = listener
    .getByRole('region', { name: 'Immediate cards' })
    .getByLabel(/Who do you play the .Overheard Conversation. card on/);
  await expect(picker).toBeVisible();
  expect((await picker.locator('option:not([disabled])').allInnerTexts()).sort()).toEqual([...neighbours].sort());

  await playImmediateCard(listener, 'OverheardConversation', neighbours[0]);

  // The holder reads the neighbour's card and nobody else is shown it; the log says who listened in, not what it saw
  const heard = await confirmIdentityCard(listener);
  expect(heard.name).toBe(neighbours[0]);
  expect((await spyNames(setup)).includes(heard.name)).toBe(heard.role === 'Spy');
  const bystander = setup.guests.get(bystanderName)!;
  await expect(bystander.getByText(/Identity card of/)).toBeHidden();

  await waitForPhase(setup, 'SelectingTeam');
  await expect(
    bystander
      .getByRole('region', { name: 'Plot cards played' })
      .getByText(`${listenerName} played “Overheard Conversation” and looks at the identity card of ${neighbours[0]}`),
  ).toBeVisible();
  await expect(bystander.getByText(/Identity card of/)).toBeHidden();
});
