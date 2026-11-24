import { mkdirSync } from 'node:fs';
import { dirname } from 'node:path';
import { request } from '@playwright/test';
import { ADMIN_PASSWORD, ADMIN_STATE_FILE, ADMIN_USERNAME, IDENTITY_URL } from './constants';

/**
 * Logs in as the seeded admin once, at the (fake) identity service, and saves the cookies, so tests do not log in
 * again. The session cookie has no Domain, so the same file works for the UI and the API.
 */
export default async function globalSetup(): Promise<void> {
  const identity = await request.newContext({ baseURL: IDENTITY_URL });
  const response = await identity.post('/api/login', { data: { username: ADMIN_USERNAME, password: ADMIN_PASSWORD } });
  if (!response.ok()) {
    throw new Error(`Admin login failed with ${response.status()}`);
  }
  mkdirSync(dirname(ADMIN_STATE_FILE), { recursive: true });
  await identity.storageState({ path: ADMIN_STATE_FILE });
  await identity.dispose();
}
