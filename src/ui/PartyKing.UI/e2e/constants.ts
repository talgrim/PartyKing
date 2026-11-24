/** The API the dev UI talks to (the `http` launch profile of PartyKing.API). */
export const API_URL = 'http://127.0.0.1:4200';
/** 127.0.0.1, not localhost: the login cookies do not reach the API from a page opened via localhost. */
export const UI_URL = 'http://127.0.0.1:2137';

/**
 * The fake identity service (`e2e/fixtures/fake-identity`) that stands in for auth.talgrim.dev. Its session cookie has no
 * Domain, so the browser sends it to the UI, the API and this server alike, as it does under .talgrim.dev.
 */
export const IDENTITY_URL = 'http://127.0.0.1:4400';

/** The demo variant of the suite (playwright.demo.config.ts) runs its own API and UI on other ports. */
export const DEMO_API_URL = 'http://127.0.0.1:4300';
export const DEMO_UI_URL = 'http://127.0.0.1:2237';

export const ADMIN_USERNAME = 'e2e-admin';
export const ADMIN_PASSWORD = 'e2e-admin-password';

/** Where global setup saves the logged-in admin cookies, reused by every test that needs the admin. */
export const ADMIN_STATE_FILE = 'e2e/.auth/admin.json';
