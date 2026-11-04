import { APIRequestContext, BrowserContext, Page, test as base, expect, request } from '@playwright/test';
import { ADMIN_STATE_FILE, API_URL } from '../constants';

export type CreatedSession = { id: string; code: string; name: string; password: string };

type Fixtures = {
  /** Creates sessions through the admin API; they are deleted when the test ends. */
  createSession: (name?: string) => Promise<CreatedSession>;
  /** A page in its own browser context (own cookies, so own player identity) that has unlocked the session. */
  openGuest: (session: CreatedSession) => Promise<Page>;
  /** Like openGuest, but the context carries the admin cookies. */
  openAdmin: (session: CreatedSession) => Promise<Page>;
};

let counter = 0;
const unique = (prefix: string) => `${prefix}-${Date.now().toString(36)}-${counter++}`;

export const test = base.extend<Fixtures>({
  createSession: async ({}, use) => {
    const admin: APIRequestContext = await request.newContext({ baseURL: API_URL, storageState: ADMIN_STATE_FILE });
    const created: CreatedSession[] = [];

    await use(async (name = unique('Party')) => {
      const password = unique('pw');
      const response = await admin.post('/admin/sessions', { data: { name, password } });
      expect(response.ok()).toBeTruthy();
      const session = { ...(await response.json()), password } as CreatedSession;
      created.push(session);
      return session;
    });

    for (const session of created) {
      await admin.delete(`/admin/sessions/${session.id}`);
    }
    await admin.dispose();
  },

  openGuest: async ({ browser }, use) => {
    const contexts: BrowserContext[] = [];
    await use(async (session) => {
      const context = await browser.newContext();
      contexts.push(context);
      const page = await context.newPage();
      await unlockSession(page, session);
      return page;
    });
    await Promise.all(contexts.map((context) => context.close()));
  },

  openAdmin: async ({ browser }, use) => {
    const contexts: BrowserContext[] = [];
    await use(async (session) => {
      const context = await browser.newContext({ storageState: ADMIN_STATE_FILE });
      contexts.push(context);
      const page = await context.newPage();
      await unlockSession(page, session);
      return page;
    });
    await Promise.all(contexts.map((context) => context.close()));
  },
});

export { expect };

/** Opens the session page and unlocks it with the password; resolves once the session itself is shown. */
export async function unlockSession(page: Page, session: CreatedSession): Promise<void> {
  await page.goto(`/s/${session.id}`);
  await page.getByLabel('Session password').fill(session.password);
  await page.getByRole('button', { name: 'Join' }).click();
  await expect(page.getByLabel('Session password')).toBeHidden();
}
