import { mkdirSync } from 'node:fs';
import { dirname } from 'node:path';
import { request } from '@playwright/test';
import { ADMIN_PASSWORD, ADMIN_STATE_FILE, ADMIN_USERNAME, API_URL } from './constants';

/** Logs in as the seeded admin once and saves the cookies, so tests do not log in again. */
export default async function globalSetup(): Promise<void> {
  const api = await request.newContext({ baseURL: API_URL });
  const response = await api.post('/admin/login', { data: { username: ADMIN_USERNAME, password: ADMIN_PASSWORD } });
  if (!response.ok()) {
    throw new Error(`Admin login failed with ${response.status()}`);
  }
  mkdirSync(dirname(ADMIN_STATE_FILE), { recursive: true });
  await api.storageState({ path: ADMIN_STATE_FILE });
  await api.dispose();
}
