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
- A failed start takes back the session and the profile. The `DemoSession` row is written before the claim, so a crash in between still ends with the session being swept.

## What the demo refuses

- Spotify login, search, Spotify links, Spotify fallback playlists: `DemoHostTokenProvider` and `DemoSpotifyService` answer 403 "<what> is not available in the demo". Nothing reaches Spotify.
- The host player asks for the Spotify token only for Spotify songs (see "Session page" in `frontend.md`), so a demo host plays YouTube links.

## Expiry and cleanup

- `DemoSession` (`Domain/Entities/Demo`) holds the session id (key, cascade from `Sessions`), the synthetic host's profile id and `ExpiresAt` (UTC). `GET /sessions/{id}/demo` (`[RequireSessionAccess]`) answers `expiresAt` and the server's clock, so the UI counts down without trusting the visitor's clock; 404 for a session that is not a demo one.
- `DemoSessionCleanupWorker` (API, a `BackgroundService` on a `PeriodicTimer`, every minute and once at startup) runs `IDemoSessionCleanup.SweepAsync` in a scope. For each expired session it deletes the session through `ISessionAdminService.DeleteAsync`, which cascades the rows and raises `SessionDeleted` (photos, player state, the pushes that tell connected clients), and then deletes the synthetic profile (`ISpotifyProfilesRepository.DeleteProfileAsync`). The session goes first: the profile's foreign key is `SetNull`, so the other order would only blank the host. A session that fails is logged and left for the next sweep.

## Limits

- `MaxSessions` is checked by counting rows before the session is made, so two simultaneous starts can overshoot it by one.
- Photos: a photo over `MaxPhotoBytes` or a batch that would take the session over `MaxPhotosPerSession` is refused with a 400 (`SlideshowService.EnsureWithinDemoLimits`, using the file count of the session's folder; uploads racing each other can overshoot by a batch).
- Tests: `DemoSessionTests` (Domain), `DemoSessionServiceTests`, `DemoSessionCleanupTests`, `DemoRefusalTests`, `DemoRegistrationTests`, `DemoSessionsRepositoryTests`, `SpotifyProfilesRepositoryTests` (Application), `DemoControllerTests` (Api).

## The UI

Code: `src/service/endpoints/demo` (API, `useDemoConfig`/`useIsDemo`, `useDemoSession`) and `components/Demo`. Nothing changes on a server where `GET /demo/config` says `enabled: false` or fails.

- `useDemoConfig` asks once. `Home` renders nothing until it is known, then either `DemoLanding` (a "Start the demo" button, no list of sessions and no fetch of them) or the ordinary list. `StartDemoButton` calls `POST /demo/sessions` and opens the session; 409 (full) and 429 (too many starts) get their own message, anything else a generic one.
- `DemoBanner` sits above the session header for everyone who got in (guests by QR too). It owns the one-second clock (`useDemoCountdown`), so the session page does not render every second. The offset between the server's clock and the device's is taken once per answer from `serverTime`; the warning styles start at 5 minutes and 1 minute.
- The end: `MusicQueue` shows `DemoEnded` ("Start a new demo") when the banner reports the time is up, or when a session that was open answers 401 (the server answers 401, not 404, for a deleted session). A session never opened is still asked for its password.
- Hidden in a demo: the account menu and the link to all sessions (`AppHeader`), the Spotify host login buttons (`SessionHeader`), the fallback playlist (`SessionSettings`). `Queue` takes links only (a plain text says so instead of searching Spotify) and shows the sample chips of `components/Demo/demoTracks.ts`.
- Games: the host opens a game to guests in a demo like anywhere else (`GameLobby.OpenAsync` is host or admin; a demo has no admin).
- The samples are copyright-free NoCopyrightSounds tracks. Check a new one with `https://www.youtube.com/oembed?url=<watch url>`: 403 means embedding is disabled and the host's player cannot play it.

