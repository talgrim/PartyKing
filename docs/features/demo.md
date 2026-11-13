# Demo mode

A server that is a demo needs no Spotify: each visitor starts a throw-away session of their own, hosts it, and plays YouTube links. It is meant to be linked from a website. The plan and the decisions behind it are in `docs/plans/demo-mode.md`.

## Contents

- Turning it on
- Starting a session
- What the demo refuses
- Expiry and cleanup
- Limits
- The UI

## Turning it on

- `DemoSettings` (`DemoConfiguration`, `Application/Configuration`; env vars `DemoSettings__Enabled` and so on). Everything is off unless `Enabled` is true. `LifetimeMinutes` (180), `MaxSessions` (50), `StartsPerHourPerIp` (5), `MaxPhotosPerSession` (10), `MaxPhotoBytes` (5 MB).
- Deployed as its own Fly app from the same image (`fly.demo.toml`; see "Demo app" in `docs/deployment.md`). Its Data Protection keys must never be production's: the demo keeps them in memory.
- The flag is read at registration time: `RegisterDemo` (Application) swaps `ISpotifyService` and `IHostTokenProvider` for the refusing `DemoSpotifyService` and `DemoHostTokenProvider`, and `RegisterDemoWorker` (API) adds the cleanup worker. The services the endpoints use are registered either way, so a server that is not a demo answers `GET /demo/config` with `enabled: false` and everything else with 404.
- `GET /sessions` answers an empty list in a demo (`SessionService`), so visitors do not see each other's sessions, and `POST /account/register` is refused (`AccountService`): accounts are permanent rows.

## Starting a session

- `POST /demo/sessions` (`DemoController`, rate limited per IP by the policy `demo-start`) calls `IDemoSessionService.StartAsync`, which returns a `DemoStart` and never sees HTTP. The controller writes the same three cookies a Spotify host login and a password join would: `GrantHost` (the profile id), `GrantSpotifyAccessToken` (a placeholder that lives as long as the session) and `GrantSessionAccess` (the access stamp, so nobody types the random password).
- The visitor is a real host to the rest of the code: the service makes a synthetic `SpotifyProfile` (`demo-<guid>`, a unique fake refresh token), creates the session through `ISessionAdminService.CreateAsync` with a random password, records a `DemoSession` and claims the session with `ISessionsRepository.TryClaimAsync`. Everything that asks for the host or an admin (games, slideshow, settings, QR code, the player) works unchanged, and `CookieAuthenticationHandler` does not call Spotify because the auth cookie is present.
- A start also writes a fourth cookie, `cookie.partyKing.demo` (`GrantDemoSession`, signed, 30 days, the last 20): the session id and its synthetic host's profile id, one entry per demo this browser started. The host cookie holds one profile id and is replaced by every start, so without it only the newest session would stay hosted.
- A failed start takes back the session and the profile. The `DemoSession` row is written before the claim, so a crash in between still ends with the session being swept.

## Returning to a session

- `GET /demo/sessions/mine` (`DemoController.GetMine`, no session access needed) hands the entries of `cookie.partyKing.demo` to `IDemoSessionService.ListStartedAsync`, which keeps the ones that are really this host profile's demo session (`DemoSession.HostProfileId` matches) and have not ended by the clock (the sweep deletes a session up to a minute late), and answers `{sessions: [{sessionId, name, expiresAt}], serverTime}`, the one that ends last first. Nothing but the browser's own cookie decides what is listed, so nobody sees another visitor's sessions, and the cookie also holds no session the visitor only joined as a guest (a QR join sets `cookie.partyKing.sessions` only). 404 on a server that is not a demo.
- `POST /demo/sessions/{id}/resume` (`DemoController.Resume`) makes the visitor the host of one of those sessions again: `IDemoSessionService.ResumeAsync` checks it the same way and returns a `DemoStart` with the time that is left, and the controller grants the same three cookies as a start (`GrantHost`, `GrantSpotifyAccessToken`, which `CookieAuthenticationHandler` needs because without it it asks the refusing `DemoSpotifyService` for a token, and `GrantSessionAccess`). 404 for a session this browser did not start, one that ended, and on a server that is not a demo. Resuming replaces the host cookie, so the session that was hosted before is a guest session until it is resumed too.
- Limit: the Data Protection keys of the demo app are in memory (`fly.demo.toml`), so after a restart every cookie is unreadable and a returning visitor sees no sessions, even while their rows exist until they expire.

## What the demo refuses

- Spotify login, search, Spotify links, Spotify fallback playlists: `DemoHostTokenProvider` and `DemoSpotifyService` answer 403 "<what> is not available in the demo". Nothing reaches Spotify.
- The host player asks for the Spotify token only for Spotify songs (see "Session page" in `frontend.md`), so a demo host plays YouTube links.

## Expiry and cleanup

- `DemoSession` (`Domain/Entities/Demo`) holds the session id (key, cascade from `Sessions`), the synthetic host's profile id and `ExpiresAt` (UTC). `GET /sessions/{id}/demo` (`[RequireSessionAccess]`) answers `expiresAt` and the server's clock, so the UI counts down without trusting the visitor's clock; 404 for a session that is not a demo one.
- `DemoSessionCleanupWorker` (API, a `BackgroundService` on a `PeriodicTimer`, every minute and once at startup) runs `IDemoSessionCleanup.SweepAsync` in a scope. For each expired session it deletes the session through `ISessionAdminService.DeleteAsync`, which cascades the rows and raises `SessionDeleted` (photos, player state, the pushes that tell connected clients), and then deletes the synthetic profile (`ISpotifyProfilesRepository.DeleteProfileAsync`). The session goes first: the profile's foreign key is `SetNull`, so the other order would only blank the host. A session that fails is logged and left for the next sweep.

## Limits

- `MaxSessions` is checked by counting rows before the session is made, so two simultaneous starts can overshoot it by one.
- Photos: a photo over `MaxPhotoBytes` or a batch that would take the session over `MaxPhotosPerSession` is refused with a 400 (`SlideshowService.EnsureWithinDemoLimits`, using the file count of the session's folder; uploads racing each other can overshoot by a batch).
- Tests: `DemoSessionTests` (Domain), `DemoSessionServiceTests` (start, list, resume), `DemoSessionCleanupTests`, `DemoRefusalTests`, `DemoRegistrationTests`, `DemoSessionsRepositoryTests`, `SpotifyProfilesRepositoryTests` (Application), `DemoControllerTests` and `SignedCookiesTests` (the demo sessions cookie) (Api).

## The UI

Code: `src/service/endpoints/demo` (API, `useDemoConfig`/`useIsDemo`, `useDemoSession`) and `components/Demo`. Nothing changes on a server where `GET /demo/config` says `enabled: false` or fails.

- `useDemoConfig` asks once. `Home` renders nothing until it is known, then either `DemoLanding` (a "Start the demo" button, no list of everybody's sessions and no fetch of them) or the ordinary list. `DemoLanding` also shows `MyDemoSessions` (`GET /demo/sessions/mine` through `useMyDemoSessions`): the visitor's own running demo sessions, each with its own `useDemoCountdown` countdown and a "Back to the party" button that calls `POST /demo/sessions/{id}/resume` and opens the session; a session disappears from the list when its time is up, and a 404 on resume says so and reloads the list. It shows nothing without a session. `StartDemoButton` calls `POST /demo/sessions` and opens the session; 409 (full) and 429 (too many starts) get their own message, anything else a generic one.
- `DemoBanner` sits above the session header for everyone who got in (guests by QR too). It owns the one-second clock (`useDemoCountdown`), so the session page does not render every second. The offset between the server's clock and the device's is taken once per answer from `serverTime`; the warning styles start at 5 minutes and 1 minute.
- The end: `MusicQueue` shows `DemoEnded` ("Start a new demo") when the banner reports the time is up, or when a session that was open answers 401 (the server answers 401, not 404, for a deleted session). A session never opened is still asked for its password.
- Hidden in a demo: the account menu and the link to all sessions (`AppHeader`), the Spotify host login buttons (`SessionHeader`), the fallback playlist (`SessionSettings`). `Queue` takes links only (a plain text says so instead of searching Spotify) and shows the sample chips of `components/Demo/demoTracks.ts`.
- Games: the host opens a game to guests in a demo (`GameLobby.OpenAsync` allows it when `DemoSettings:Enabled`; an admin does it elsewhere, and a demo has none). The Ringo and Resistance boards show "Open the game" to the host there (`useIsDemo`).
- The samples are copyright-free NoCopyrightSounds tracks. Check a new one with `https://www.youtube.com/oembed?url=<watch url>`: 403 means embedding is disabled and the host's player cannot play it.

