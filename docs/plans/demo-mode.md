# Demo mode implementation plan

A preview of PartyKing that can be linked from the owner's website without Spotify access (the Spotify app is stuck in
development mode). Status: **planned**; Steps 0 and 1 are the first to be done.

## Decisions

- **Entry:** a per-visitor throw-away session, created from a "Start demo" button.
- **Audio:** YouTube only, with real playback. Spotify search, links and playlists are not available in the demo.
- **Lifetime:** fixed 3 hours, with a countdown in the UI so visitors know when the session ends.
- **Photos:** capped at 10 photos and 20 MB per session.
- **Samples:** about 8 royalty-free YouTube tracks as one-tap "add" chips.
- **Hostname:** `demo.party.talgrim.dev`.
- **Deployment:** a separate Fly app built from the same image; the demo flag is a runtime setting (`Demo:Enabled`), not
  a build flag.
- **Not an iframe:** nginx sends `X-Frame-Options: DENY`; the website links to the demo.

## What works without Spotify (no change needed)

Session list, password and QR join, the queue with YouTube links (keyless oEmbed), lyrics (LRCLIB), photo upload and
slideshow, Ringo, Resistance, Never have I ever (the LLM falls back to built-in lists without a key), accounts, the BAC
calculator and i18n. Spotify is only involved in host login, search and track lookup, the Spotify-playlist fallback and
the `/spotify/token` handout.

The gap is that every manager action (games, slideshow, settings, QR, the player) needs the Spotify-backed host or an
admin. The demo therefore gives each visitor a synthetic host.

## Step 0: Spikes (done)

1. Does a fake host work with the cookie handler? **Yes**: `CookieAuthenticationHandler` only calls Spotify when the
   `cookie.partyKing.auth` cookie is missing; with it present the claims come straight from the cookies. The demo
   endpoint must set the `user` and `auth` cookies.
2. Does the `DataProtection` startup rule need a certificate on the demo app? **No**: the certificate is required only
   when `DataProtection:KeysDirectory` is set. Without it the keys are not persisted, so a restart invalidates the
   cookies, which is acceptable for throw-away sessions. Persisting them (directory plus certificate) is optional.
3. Do `HostSessionCleanup` and the `SessionDeleted` handlers cope with a synthetic `SpotifyProfile`? **Yes**:
   `HostSessionCleanup` only clears in-memory state and invalidates the token cache by profile id. Removing the
   synthetic profile row itself is part of the cleanup service in Step 2 (the `Sessions.HostProfileId` foreign key
   means the session must be deleted first).

## Step 1: Relax the Spotify gate in the host player (done, uncommitted)

- `HostPlayer.tsx` asks for the Spotify token, and mounts `SpotifyPlayer`, only once a Spotify song is current, now
  playing or queued (and keeps it from then on). Start is disabled only while a Spotify song needs a token that has not
  arrived.
- A 401 for the token goes to the Spotify login while the music has not started; once it plays it shows
  `hostPlayer.spotifyLoginExpired` instead, so a Spotify song queued mid-party never redirects the host away.
- Tests in `HostPlayer.test.tsx` ("without a Spotify login"): YouTube plays without a token request, Start waits for
  the token when a Spotify song is queued, the login redirect before the start, and no redirect while music plays.
- Docs: `docs/frontend.md` (session page) and `docs/features/music.md` (`GET /spotify/token`).

## Step 2: Backend

| Layer | Work |
|---|---|
| Config | `DemoConfiguration` (`Demo:Enabled`, `LifetimeMinutes` = 180, `MaxSessions`, `MaxPhotosPerSession` = 10, `MaxPhotoBytes` = 20 MB). Off by default. |
| Domain | `DemoSession` entity (session id, host profile id, `ExpiresAt`) and an `IDemoSessionsRepository` port. |
| Persistence | A `DemoSessions` table with a cascade from `Sessions`, and a migration (`AddedDemoSessions`). |
| Infrastructure | The repository (singleton, `DbContext` factories, wrapped with `AddMetered`). `DemoSpotifyIntegrationClient` in `Infrastructure.Spotify`, whose methods all fail with "not available in the demo". |
| Application | `DemoHostTokenProvider` (same error). `IDemoSessionService.StartAsync` creates the session with a random name and password, creates a synthetic "Demo host" profile, claims the session and stores `ExpiresAt`; it returns what happened, including "capacity reached", and never sees HTTP. The photo quota check goes in the upload service. |
| Cleanup | `DemoSessionCleanupService` (`BackgroundService`, `PeriodicTimer` 1 min, plus a sweep at startup) calls `ISessionAdminService.DeleteAsync` for expired sessions and then removes the synthetic profile. Uploads, state and the "game ended" push come from the existing handlers. |
| API | `DemoController`, registered only when the flag is on, with a guard so `/demo` returns 404 otherwise. `POST /demo/sessions` writes the unlock, user and auth cookies (rate limit `demo-create`, about 3 per hour per IP); `GET /demo/config` returns `enabled` and `serverTime`; `GET /sessions/{id}/demo` returns `expiresAt` (404 when not a demo session). In demo, `GET /sessions` returns an empty list and account registration is rejected. DI swaps live in `Program.cs` / `DependencyInjection.cs` under the flag. |

Tests (separate commits): the `DemoSession` expiry rule (Domain); the repository, `StartAsync` including the capacity
cap and the photo quota, and the cleanup with `FakeClock` (Application, SQLite); the controller, cookies, the 404 guard
when disabled and the empty list (Api).

## Step 3: Frontend demo mode

- `service/endpoints/demo/demoApi.ts` and `useDemoConfig` (SWR). Nothing changes when the flag is off.
- **Home:** in demo mode a landing with "Start demo" instead of the session list; the button calls
  `POST /demo/sessions` and navigates to `/s/:id`.
- **Banner:** `DemoBanner` with a countdown. The clock offset is computed once from `serverTime`, then the countdown
  ticks locally (a wrong phone clock does not skew it). Warnings at 5 min and 1 min. Guests who joined by QR see it too.
- **Ended screen:** at zero, or on a session-deleted push or 404, show "Demo ended" with "Start a new demo". The
  countdown logic is a pure hook (`useDemoCountdown`).
- **Hide Spotify UI** in demo: the "Login as host" button (`SessionHeader`), the search hint (`Queue`), the Spotify
  part of `FallbackPlaylistForm`, the takeover control.
- **Sample chips:** `demoTracks.ts` with about 8 royalty-free YouTube links, each verified with the oEmbed endpoint
  (videos with embedding disabled are rejected).
- **i18n:** every new string in both `en.json` and `pl.json`.
- **Tests:** vitest for the countdown (fake timers, offset, thresholds), the landing, the banner and the hidden elements.

## Step 4: E2E

A Playwright variant that starts the API with `Demo__Enabled=true` and a short `Demo__LifetimeMinutes` override.
Spec 1: start the demo, the banner appears, a sample track queues, a second context joins by QR and starts a game.
Spec 2: the session expires, "Demo ended" appears, and a new demo can start. Run once at the end (about 8 min).

## Step 5: Deployment and docs

- `fly.demo.toml`: app `partyking-demo`, region `fra`, its own `/data` volume, `auto_stop_machines = 'stop'` with
  `min_machines_running = 0`, `Demo__Enabled=true`. No Spotify, admin or LLM secrets. Its own Data Protection keys.
- `deploy.yml`: a second job deploying with `--config fly.demo.toml` on push to `master`, after the same CI gate.
- Manual: the `demo.party.talgrim.dev` certificate and DNS record in Fly.
- Docs: `docs/features/demo.md`, a path-scoped rule in `.claude/rules/`, a row in the CLAUDE.md table, a "second app"
  section in `docs/deployment.md`.

## Commit sequence

1. `feat(ui)`: relax the Spotify gate in the host player, then its tests.
2. `feat(domain, persistence)`: `DemoSession` and the migration.
3. `feat(application, infrastructure)`: the demo service, fakes, cleanup and quota.
4. `feat(api)`: the controller, guard and rate limit, then the backend tests.
5. `feat(ui)`: the demo landing, banner, countdown and chips, then the UI tests.
6. `test(e2e)`: the demo specs.
7. `ci`, `docs`: `fly.demo.toml`, the deploy job and the docs.

## Risks

- **Abuse:** the demo is public; the rate limit, the session cap and the photo quota are the protection. Nothing in it
  is persistent (no admin, no accounts).
- **Cold start:** after scale-to-zero the first visit takes a few seconds.
- **Phones:** the visitor taps Start once (autoplay needs a gesture) and the host tab must stay open. YouTube embeds
  need a real domain, not a raw IP (player error 150).
