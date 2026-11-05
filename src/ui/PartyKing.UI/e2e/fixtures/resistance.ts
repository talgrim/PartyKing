import { Page } from '@playwright/test';
import { CreatedSession, expect } from './test';

export const PLAYER_NAMES = ['Anna', 'Bartek', 'Celina', 'Darek', 'Ela'];

export type ResistanceGameSetup = {
  admin: Page;
  /** The players' pages by name (each its own browser context). */
  guests: Map<string, Page>;
};

type Openers = {
  openAdmin: (session: CreatedSession) => Promise<Page>;
  openGuest: (session: CreatedSession) => Promise<Page>;
};

/** The admin creates and opens a Resistance game, five guests join it and the admin starts it. */
export async function startResistanceGame(session: CreatedSession, { openAdmin, openGuest }: Openers): Promise<ResistanceGameSetup> {
  const admin = await openAdmin(session);
  await admin.goto(`/s/${session.id}/games`);
  await admin.getByRole('button', { name: 'Create' }).click();
  await admin.getByRole('button', { name: 'Open the game' }).click();

  const guests = new Map<string, Page>();
  for (const [index, name] of PLAYER_NAMES.entries()) {
    const guest = await openGuest(session);
    await guest.goto(`/s/${session.id}/games/resistance/join`);
    await guest.getByLabel('Your name').fill(name);
    await guest.getByRole('button', { name: 'Join' }).click();
    guests.set(name, guest);
    await expect(admin.getByText(`${index + 1}/10 players`)).toBeVisible();
  }

  await admin.getByRole('button', { name: 'Start game' }).click();
  for (const guest of guests.values()) {
    await expect(guest.getByRole('button', { name: 'Show your role' })).toBeVisible();
  }
  return { admin, guests };
}

/** Roles and the first leader are random, so the tests read them from the players' own screens. */
export async function findPlayer(
  guests: Map<string, Page>,
  has: (page: Page) => Promise<boolean>,
): Promise<[string, Page]> {
  let found: [string, Page] | undefined;
  await expect
    .poll(async () => {
      for (const entry of guests) {
        if (await has(entry[1])) {
          found = entry;
          return true;
        }
      }
      return false;
    })
    .toBe(true);
  return found!;
}

/** The leader is the only player whose screen offers the team picker. */
export const isLeader = (page: Page) =>
  page.getByRole('button', { name: 'Submit the team for a vote' }).isVisible();

/** Turns the role card face up and says whether this player is a spy (only spies see their fellows). */
export async function isSpy(page: Page): Promise<boolean> {
  const card = page.getByRole('button', { name: 'Show your role' });
  if (await card.isVisible()) {
    await card.click();
  }
  await expect(page.getByRole('button', { name: 'Hide your role' })).toBeVisible();
  return page.getByRole('heading', { name: 'Your fellow spies' }).isVisible();
}
