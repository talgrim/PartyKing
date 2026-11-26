import { BrowserContext } from '@playwright/test';

/** The key `PrivacyNotice` keeps its "seen" flag under. */
export const PRIVACY_NOTICE_KEY = 'partyking.privacyNotice.v1';

/**
 * The notice is fixed to the bottom of the screen and would sit over what a test clicks, so every context starts with
 * it already dismissed. Only the spec that is about the notice (`privacy-notice` in `site-bar.spec.ts`) uses a context without.
 */
export const dismissPrivacyNotice = async (context: BrowserContext): Promise<void> => {
  await context.addInitScript((key) => {
    try {
      window.localStorage.setItem(key, 'true');
    } catch {
      // No storage on this page (an about:blank): nothing to dismiss
    }
  }, PRIVACY_NOTICE_KEY);
};
