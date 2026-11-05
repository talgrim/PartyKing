# E2E tests (Playwright, Chromium)

From `src/ui/PartyKing.UI`: `npm run e2e`, about 1.5 min, 22 tests. First time only: `npx playwright install chromium`. Typecheck the suite with `npx tsc -p e2e/tsconfig.json`; `vite.config.ts` keeps `e2e` and `playwright.config.*` out of vitest.

## Setup

- `playwright.config.ts` starts both servers itself and never reuses running ones, so stop your dev API and Vite first (ports 4200 and 2137).
- `e2e/start-api.mjs` runs the API in Development on a throw-away database. The web root and the *content root* are in the git-ignored `e2e/.work` (wiped on each start), so your `appsettings.Local.json` (which would override env vars) is not read. Admin: `e2e-admin` / `e2e-admin-password`. `RateLimiting__Disabled=true` — honoured in Development only (`Program.cs` skips `UseRateLimiter`), because the suite logs in and unlocks sessions far more often than the 10/min limits allow. Then `npm start` runs Vite.
- The e2e API runs without `LlmSettings:ApiKey` on purpose, so the specs also guard that Ringo works without one.
- `global-setup.ts` logs the admin in once and saves the cookies to `e2e/.auth` (git-ignored).
- One worker; tests share one API and database.
- The base URL must be `http://127.0.0.1:2137`, not `localhost` (the login cookies don't reach the API).
- Selectors are roles and labels with the English texts.

## Fixtures (`e2e/fixtures/test.ts`)

- `createSession` creates sessions through the admin API and deletes them after the test; `unlockSession` types the password.
- `openAdmin` / `openGuest` give each player their own browser context (own cookies, so own player identity; no `?persona=` needed).
- `e2e/fixtures/resistance.ts`: `startResistanceGame` — the admin creates, opens and starts a game with five guests.

## Specs

- Home smoke; admin login and session creation; wrong-password and right-password unlock; the language switch.
- Photo upload (and a refused fake image).
- `suggestions.spec.ts`: Never have I ever statements and Ringo ace rules come from the built-in lists with the "AI is unavailable" notice. The Ringo one flips cards as the admin until an ace shows, since the deck is random.
- `session-settings.spec.ts`: rename shows on Home; a password change sends guests back to the password form but keeps the admin in.
- `join-qr.spec.ts`: the token is read from the `join-token` response, because the dialog shows only a QR image; a guest opening `?join=<token>` gets in; a bad token shows the password form.
- `guest-access.spec.ts`: a guest has no settings or QR button, and `/s/:id/games` redirects to the session.
- `photos-admin.spec.ts`: an admin sees the empty slideshow and a disabled "Download as ZIP"; after an upload, the photo in the slideshow, the count in Session settings and a real zip download. The slideshow `img` has no alt text, so it is found by `.photos-slideshow img`.
- `resistance-lobby.spec.ts`: an admin creates and opens the game, five guests join and the admin starts it; each player gets a role card, the admin none, a latecomer is told it started.
- `ringo-lobby.spec.ts`: two guests join, only the player on turn can flip, the flip and "Continue" work. Ringo's cards are scattered at random and overlap, so the spec sends the click to the element (`dispatchEvent('click')`) and not through the pointer.
- `resistance-round.spec.ts`: a full game of three successful missions to "Game over", and a rejected team that passes the lead on.
- `resistance-seat-takeover.spec.ts`: the host releases the seat of a player whose context was closed (needs the presence grace of 10 s, so the test waits up to 30 s for the button to enable); a new context claims it from the "Back in the game" picker; an unreleased seat is not offered.
- Not covered: anything needing Spotify or YouTube (host login, the player, search), the plot cards add-on, and a game won by the spies.

## Resistance helpers

Roles and the first leader are random, so the helpers read them from the players' screens:
- The leader is the one who has the "Submit the team for a vote" button; a spy is the one who sees "Your fellow spies" after turning the role card.
- The team picker's checkbox is covered by a tick, so the test taps the player's name inside the "Pick the team" group.
- The Approve/Reject/Success/Fail cards are `role="button"` SVGs inside the "Voting" and "Mission" regions.
- The mission panel disappears with the last card, so the tests do not wait for "Card played".

## CI

The `e2e` job of `ci.yml`: setup-dotnet and setup-node, `npx playwright install --with-deps chromium`, `npm run e2e`. With `CI` set Playwright retries a failure once and writes an HTML report, uploaded with the traces as an artifact on failure. `deploy.yml` reuses `ci.yml`, so a failing e2e job also blocks a deploy.
