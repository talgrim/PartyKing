import { Page } from '@playwright/test';
import { API_URL } from '../constants';
import { CreatedSession, expect } from './test';

export const PLAYER_NAMES = ['Anna', 'Bartek', 'Celina', 'Darek', 'Ela', 'Franek', 'Grażyna', 'Henryk', 'Irena', 'Janusz'];

export type ResistanceGameSetup = {
  session: CreatedSession;
  admin: Page;
  /** The players' pages by name (each its own browser context). */
  guests: Map<string, Page>;
};

type Openers = {
  openAdmin: (session: CreatedSession) => Promise<Page>;
  openGuest: (session: CreatedSession) => Promise<Page>;
};

type StartOptions = {
  /** How many guests join before the admin starts the game (5 to 10). */
  players?: number;
  /** Turns the Plot cards add-on on before the start (it cannot be changed afterwards). */
  plotCards?: boolean;
};

/** The admin creates and opens a Resistance game, the guests join it and the admin starts it. */
export async function startResistanceGame(
  session: CreatedSession,
  { openAdmin, openGuest }: Openers,
  { players = 5, plotCards = false }: StartOptions = {},
): Promise<ResistanceGameSetup> {
  const admin = await openAdmin(session);
  await admin.goto(`/s/${session.id}/games`);
  await admin.getByRole('button', { name: 'Create' }).click();
  await admin.getByRole('button', { name: 'Open the game' }).click();

  const guests = new Map<string, Page>();
  for (const [index, name] of PLAYER_NAMES.slice(0, players).entries()) {
    const guest = await openGuest(session);
    await guest.goto(`/s/${session.id}/games/resistance/join`);
    await guest.getByLabel('Your name').fill(name);
    await guest.getByRole('button', { name: 'Join' }).click();
    guests.set(name, guest);
    await expect(admin.getByText(`${index + 1}/10 players`)).toBeVisible();
  }

  if (plotCards) {
    await enableModule(admin, 'Plot cards');
  }
  await admin.getByRole('button', { name: 'Start game' }).click();
  for (const guest of guests.values()) {
    await expect(guest.getByRole('button', { name: 'Show your role' })).toBeVisible();
  }
  return { session, admin, guests };
}

/** Turns an add-on on in the "Game settings" dialog. It has to happen before the game starts. */
export async function enableModule(admin: Page, module: 'Plot cards'): Promise<void> {
  await admin.getByRole('button', { name: 'Game settings' }).click();
  const dialog = admin.getByRole('dialog', { name: 'Game settings' });
  const checkbox = dialog.getByRole('checkbox', { name: `Enable ${module}` });
  // Controlled by the server state, so it only ticks after the round trip (`check()` would not wait for that)
  await checkbox.click();
  await expect(checkbox).toBeChecked();
  await dialog.getByRole('button', { name: 'Close' }).click();
  await expect(dialog).toBeHidden();
}

// ---------------------------------------------------------------------------------------------------------------------
// What a player may know, read from the API with that player's own cookies
//
// Roles and the first leader are random, so the helpers need to know who is who. The game endpoint answers with the
// view of the caller, so asking it through a player's browser context gives exactly what that player's screen is built
// from (and, for the hidden-information specs, what it must not contain). It is only used to find who has to act: the
// acting itself always goes through the UI.
// ---------------------------------------------------------------------------------------------------------------------

type Phase =
  | 'SelectingTeam'
  | 'Voting'
  | 'VoteResult'
  | 'GameOver'
  | 'PlayingMission'
  | 'MissionResult'
  | 'DealingCards'
  | 'ResolvingCards'
  | 'LeaderWindow'
  | 'ConfidenceWindow'
  | 'PeekWindow';

type Role = 'Resistance' | 'Spy';

export type PlotCardType =
  | 'OverheardConversation'
  | 'OpinionMaker'
  | 'EstablishConfidence'
  | 'OpenUp'
  | 'NoConfidence'
  | 'KeepingCloseEye'
  | 'StrongLeader'
  | 'InTheSpotlight'
  | 'TakeResponsibility';

/** What the cards are called on screen. */
export const PLOT_CARD_NAMES: Record<PlotCardType, string> = {
  OverheardConversation: 'Overheard Conversation',
  OpinionMaker: 'Opinion Maker',
  EstablishConfidence: 'Establish Confidence',
  OpenUp: 'Open Up',
  NoConfidence: 'No Confidence',
  KeepingCloseEye: 'Keeping a Close Eye on You',
  StrongLeader: 'Strong Leader',
  InTheSpotlight: 'In the Spotlight',
  TakeResponsibility: 'Take Responsibility',
};

/** The part of `ResistanceGameDto` the specs look at. */
export type ResistanceView = {
  phase: Phase | null;
  startedAt: string | null;
  players: { id: string; name: string }[];
  yourPlayerId: string | null;
  leaderPlayerId: string | null;
  yourRole: Role | null;
  fellowSpies: string[] | null;
  missionNumber: number;
  requiredTeamSize: number | null;
  requiredFails: number | null;
  teamPlayerIds: string[];
  rejectedTeams: number;
  winner: Role | null;
  votedPlayerIds: string[];
  yourVote: boolean | null;
  votes: { playerId: string; approve: boolean }[] | null;
  revealedRoles: { playerId: string; role: Role }[] | null;
  missionResults: unknown[];
  /** The cards this player holds (the leader's `toDeal` ones are drawn and still have to be dealt). */
  yourPlotCards: { id: number; type: PlotCardType; toDeal: boolean; inPlay: boolean }[];
  /** Public: how many cards each player holds (an Opinion Maker in play is not counted). */
  plotCardCounts: { playerId: string; count: number }[];
  opinionMakerPlayerIds: string[];
  playedPlotCards: { id: number; type: PlotCardType; holderPlayerId: string; targetPlayerId: string | null }[];
  leaderWindowPassedPlayerIds: string[];
};

/** The game as this player's page sees it. */
export async function viewOf(page: Page, session: CreatedSession): Promise<ResistanceView> {
  const response = await page.request.get(`${API_URL}/sessions/${session.id}/games/resistance`);
  expect(response.ok()).toBeTruthy();
  return (await response.json()) as ResistanceView;
}

/** The game as one of the players sees it. */
export const viewOfPlayer = (setup: ResistanceGameSetup, name: string) =>
  viewOf(setup.guests.get(name)!, setup.session);

export const nameOf = (view: ResistanceView, id: string) => view.players.find((player) => player.id === id)!.name;

/** Waits until the game is in `phase` (it may still be on its way after the last click) and returns the view. */
export async function waitForPhase(setup: ResistanceGameSetup, phase: Phase): Promise<ResistanceView> {
  const [, page] = [...setup.guests][0];
  let view!: ResistanceView;
  await expect
    .poll(async () => {
      view = await viewOf(page, setup.session);
      return view.phase;
    })
    .toBe(phase);
  return view;
}

/** Waits for the next team selection and returns who leads it. */
export async function waitForLeader(setup: ResistanceGameSetup): Promise<[string, Page]> {
  const view = await waitForPhase(setup, 'SelectingTeam');
  const name = nameOf(view, view.leaderPlayerId!);
  return [name, setup.guests.get(name)!];
}

/** The names of the spies (every player is asked, so a wrong view of one of them would show). */
export async function spyNames(setup: ResistanceGameSetup): Promise<string[]> {
  const spies: string[] = [];
  for (const name of setup.guests.keys()) {
    if ((await viewOfPlayer(setup, name)).yourRole === 'Spy') {
      spies.push(name);
    }
  }
  return spies;
}

// ---------------------------------------------------------------------------------------------------------------------
// Playing a round through the UI
// ---------------------------------------------------------------------------------------------------------------------

/** The leader taps the given players in the team picker and submits the team. */
export async function submitTeam(leader: Page, names: string[]): Promise<void> {
  const picker = leader.getByRole('group', { name: 'Pick the team' });
  for (const name of names) {
    // The checkbox itself is visually hidden under a tick; the label text is what a person taps
    await picker.getByText(name, { exact: true }).click();
    await expect(picker.getByRole('checkbox', { name, exact: true })).toBeChecked();
  }
  await leader.getByRole('button', { name: 'Submit the team for a vote' }).click();
}

export const vote = (page: Page, choice: 'Approve' | 'Reject') =>
  page.getByRole('region', { name: 'Voting' }).getByRole('button', { name: choice }).click();

/** Every player casts the same vote. */
export async function voteAll(setup: ResistanceGameSetup, choice: 'Approve' | 'Reject'): Promise<void> {
  for (const page of setup.guests.values()) {
    await vote(page, choice);
  }
}

/** The leader of a finished mission starts the next one. */
export async function continueMission(leader: Page): Promise<void> {
  const next = leader.getByRole('button', { name: 'Next', exact: true });
  await expect(next).toBeVisible();
  await next.click();
  await expect(next).toBeHidden();
}

/**
 * Plays one whole mission: the leader picks a team that gets the outcome (a failed mission needs the required number
 * of spies on it, a successful one has none), everybody approves and the team plays its cards. The leader then
 * continues unless that was the last mission. Returns the view once the mission is resolved.
 */
export async function playMission(setup: ResistanceGameSetup, outcome: 'success' | 'fail'): Promise<ResistanceView> {
  const [, leader] = await waitForLeader(setup);
  const view = await viewOf(leader, setup.session);
  const spies = await spyNames(setup);
  const resistance = [...setup.guests.keys()].filter((name) => !spies.includes(name));

  const failers = outcome === 'fail' ? spies.slice(0, view.requiredFails!) : [];
  const team = [...failers, ...resistance].slice(0, view.requiredTeamSize!);
  await submitTeam(leader, team);
  await voteAll(setup, 'Approve');

  for (const name of team) {
    const mission = setup.guests.get(name)!.getByRole('region', { name: 'Mission' });
    // The panel goes away with the last card (it resolves the mission), so only the clicks are made here
    await mission.getByRole('button', { name: failers.includes(name) ? 'Fail' : 'Success' }).click();
  }

  await expect
    .poll(async () => (await viewOf(leader, setup.session)).phase)
    .toMatch(/MissionResult|GameOver/);
  const resolved = await viewOf(leader, setup.session);
  if (resolved.phase === 'MissionResult') {
    await continueMission(leader);
  }
  return resolved;
}

/** Every player but the leader keeps turning teams down until the game is lost. */
export async function rejectTeams(setup: ResistanceGameSetup, count: number): Promise<void> {
  for (let rejected = 0; rejected < count; rejected++) {
    const [leaderName, leader] = await waitForLeader(setup);
    const view = await viewOf(leader, setup.session);
    const others = [...setup.guests.keys()].filter((name) => name !== leaderName);
    await submitTeam(leader, others.slice(0, view.requiredTeamSize!));
    await voteAll(setup, 'Reject');
  }
}

// ---------------------------------------------------------------------------------------------------------------------
// Plot cards
//
// The e2e API is seeded (`Resistance__Seed`), so the same number of players gets the same cards in the same order. The
// specs still ask the leader's view what was drawn and fail loudly when it is not what they were written for, instead
// of hard-coding who holds what.
// ---------------------------------------------------------------------------------------------------------------------

/** Waits for the leader to have to deal the drawn cards and returns who the leader is and what was drawn. */
export async function waitForDealing(setup: ResistanceGameSetup): Promise<{ name: string; page: Page; drawn: PlotCardType[] }> {
  const view = await waitForPhase(setup, 'DealingCards');
  const name = nameOf(view, view.leaderPlayerId!);
  const page = setup.guests.get(name)!;
  const mine = await viewOf(page, setup.session);
  return { name, page, drawn: mine.yourPlotCards.filter((card) => card.toDeal).map((card) => card.type) };
}

/** The leader gives each drawn card to a player (card type to the name of the receiver) and deals them. */
export async function dealPlotCards(leader: Page, gifts: Partial<Record<PlotCardType, string>>): Promise<void> {
  const dealer = leader.getByRole('region', { name: 'Deal the Plot cards' });
  for (const [type, receiver] of Object.entries(gifts) as [PlotCardType, string][]) {
    await dealer.getByLabel(`Who gets the “${PLOT_CARD_NAMES[type]}” card`).selectOption({ label: receiver });
  }
  await dealer.getByRole('button', { name: 'Deal the cards' }).click();
  await expect(dealer).toBeHidden();
}
