# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

PartyKing is a party app: guests upload photos that the API serves as a timed slideshow, plus music sessions: each session has its own queue (YouTube links and Spotify tracks, persisted in SQLite) and its own host, who plays it through the Spotify Web Playback SDK. It has a .NET 9 backend (`src/api/PartyKing.sln`) orchestrated by .NET Aspire, and a React + Vite frontend in `src/ui/PartyKing.UI`.
## Commands

Backend (run from `src/api`):
- Build: `dotnet build PartyKing.sln`
- Run everything (API + UI via Aspire): `dotnet run --project PartyKing.AppHost`
- Run the API only: `dotnet run --project PartyKing.API` (the `http` launch profile binds to `http://127.0.0.1:4200`)
- Add an EF migration: `dotnet ef migrations add <Name> --project PartyKing.Persistence --startup-project PartyKing.Persistence --context WriterDbContext`
- Migrations are applied automatically at API startup (`MigrateDb()` in `Program.cs`). The SQLite file is `PartyKing.db`, relative to the working directory.

Frontend (from `src/ui/PartyKing.UI`):
- `npm install`, then `npm start` (Vite dev server on port 2137, host `0.0.0.0`, polling watch for WSL2/Windows HMR)
- `npm run build`
- `npm test` runs `vitest run`. To run a single test: `npx vitest run src/path/to/file.test.ts`. Tests run in jsdom with `testSetup.ts` (jest-dom matchers). `src/App.test.js` is a stale CRA leftover and fails, so run the Music tests with `npx vitest run src/components/Music`.

Backend tests: `dotnet test PartyKing.Application.Tests` (from `src/api`) (xunit; currently covers `SongInputParser`).

## Architecture

Project dependencies (layered, top → bottom):
- **AppHost**: Aspire host. Starts `PartyKing.API` and the UI as an npm app on port 2137.
- **API**: controllers plus `Program.cs`. CORS allows any origin with credentials. Static files from `wwwroot` serve the images (`wwwroot/SampleImages/{Uploaded,Placeholder}`).
- **Application**: services (`SlideshowService`, `QueueService`, `SessionService`, `AdminService`, `SpotifyService`, `HostTokenProvider`), options binding (`SlideshowConfiguration` ← `SlideshowSettings`, `AdminConfiguration` ← `AdminSettings`), and DTO mappings. References Contract and Infrastructure.
- **Contract**: public DTOs under `V1/`, returned by controllers.
- **Infrastructure**: repositories that wrap EF Core. References Persistence and Domain.
- **Persistence**: EF Core SQLite `DbContext`s, entity configurations, migrations, seed data.
- **Domain**: entities (`SlideshowImage`, `SlideshowSettings`, `QueueItem`, `SpotifyProfile`, `Session`, `AdminAccount`, `OAuthState`), enums, read/write models.
- **ServiceDefaults**: Aspire OpenTelemetry/health/resilience defaults (`AddServiceDefaults`, `MapDefaultEndpoints`).

Each layer registers its own services through a `DependencyInjection.cs` extension (`RegisterApplication`, `RegisterInfrastructure`), and `PartyKing.API/DependencyInjection.cs` chains them.

Key patterns and gotchas:
- **Reader/Writer DbContexts**: `ReaderDbContext` and `WriterDbContext` both derive from `PartyKingDbContextBase`, which hardcodes `UseSqlite("Data Source=PartyKing.db")` in `OnConfiguring`. They're registered with `AddDbContextFactory`. Repositories are **singletons** and create short-lived contexts through the factories. Migrations target `WriterDbContext`, and the snapshot file is `WriterDbContextModelSnapshot.cs`.
- **Stateful singletons**: `SlideshowService` and `SlideshowImagesRepository` are singletons that hold in-memory state: the current image and its expiration time, loaded placeholders, and current settings. `SlideshowController`'s constructor kicks off `InitializeAsync` without awaiting it (fire-and-forget) if the repository isn't initialized yet. This scans the placeholder directory under `WebRootPath` and loads settings from the DB.
- **Slideshow flow**: `get-photo` returns the current image until `SlideTime` expires. It then advances to the next uploaded image from the DB, falling back to a random placeholder. When an image with `DeleteAfterPresentation` is rotated out, it gets deleted.
- **Settings live in the DB**: slide time and other settings are stored in the `SlideshowSettings` table (migration `MovedConfigurationToDatabase`) and edited through `get-configuration`/`update-configuration`. Directory paths still come from appsettings.
- **Errors**: `CoreController` maps `ErrorOr` errors to ProblemDetails (Validation→400, Unauthorized→401, NotFound→404, Conflict→409).
- **Sessions**: a `Session` (GUID id, unique short `Code` from `SessionCodeGenerator`, name defaulting to "New session", nullable `HostProfileId`) owns one `QueueItem` queue (`QueueItem.SessionId`, cascade delete). Only an admin creates, renames, deletes or releases sessions (`/admin/sessions...`). `GET /sessions` and `GET /sessions/{id}` are public. The slideshow is still global, not per session.
- **Music queue**: `QueueItem`s (YouTube or Spotify) live in the `QueueItems` table, managed by the singleton `QueueRepository`, which has no in-memory queue state except a per-session advance lock. Everything is scoped by session: guests add songs without logging in via `POST /sessions/{id}/songs` (body `{input}`: a Spotify track URL/URI or a YouTube URL, parsed by `SongInputParser`) and search Spotify with `GET /sessions/{id}/tracks?query=`. For YouTube links `SongInputParser` extracts the video id (playlists, channels and searches are rejected), and `QueueService` looks the video up through the keyless oEmbed endpoint (`IYoutubeOEmbedClient` in `PartyKing.Infrastructure.Youtube`): the title, author and thumbnail fill the `QueueItem`, a video whose owner disabled embedding is rejected with a 400, and when YouTube can't be reached the link is queued with the URL as its title. `GET /sessions/{id}/songs` lists the queued items only: the song being played (status `Playing`) is not among them, it comes from `GET /sessions/{id}/now-playing`. `POST /sessions/{id}/songs/next?currentSongId=` is host-only: `[AuthorizeByCookie]` plus a check that the cookie's profile id is the session's `HostProfileId` (403 otherwise). `currentSongId` is the song the player thinks is playing (none when omitted); `QueueRepository.AdvanceAsync` advances only when that is really the `Playing` item, otherwise it advances nothing and returns the song that is playing. That keeps the host's end-of-song call from skipping a song an admin already skipped. `GET /spotify/token` needs a host login but is per browser, not per session.
- **Player state and remote control**: the music plays in the host's browser tab (`HostPlayer`), so the server only knows what that tab reports. `PlayerStateStore` (in-memory singleton, behind `IPlayerStateStore`) keeps per session what the host's player last reported (song id, paused, time); the player counts as connected when it reported within 10 s. The host's player calls `POST /sessions/{id}/player/state` ({songId, paused}, host-only) every ~2 s and on every change, and the response carries the one pending admin command (`Pause`/`Resume`). `GET /sessions/{id}/now-playing` (public, `PlayerService`) returns the playing song (title, artists, album art or thumbnail), `paused` (only if reported for that very song) and `playerConnected`. An admin who is not the host has no host cookie, so `POST /admin/sessions/{id}/player/commands` queues a pause/resume command and `POST /admin/sessions/{id}/songs/next` advances the queue; both answer 409 when no player is connected, and the host's player picks a skipped-to song up by following `now-playing`. `GET /sessions/{id}/me` (public, accepts either cookie) returns `{isHost, isAdmin}` so the UI knows whether to show controls.
- **Session host**: one host per session, and one Spotify account can host one session (unique index on `Session.HostProfileId`). The host flow: `POST /sessions/{id}/host-login-urls` stores a short-lived `OAuthState` (the OAuth `state` parameter, single use) and returns the Spotify URL; Spotify redirects to the fixed `/callback`, which posts `{code, state}` to `POST /authenticate`; `SessionsRepository.TryClaimAsync` then atomically claims the session (first account wins, others get 409, the current host logging in again is fine). An admin can release the host (`DELETE /admin/sessions/{id}/host`). `HostTokenProvider` (singleton) caches and refreshes one access token per host profile, resolved through the session, and returns `Error.Unauthorized` when the refresh token is dead (the host logs in again). Spotify profile ids are strings, and `SpotifyProfile.DisplayName` is shown as "Current Host". The OAuth redirect URI uses `127.0.0.1`, so log in via `http://127.0.0.1:2137/...`, not `localhost`. Known dev limitation: YouTube refuses to play embeds (player error 150) when the page's origin is an IP address such as `127.0.0.1`, while it works from `localhost`. Spotify login needs `127.0.0.1`, so YouTube songs can't play in local dev; it goes away when the app is deployed on a real domain.
- **Cookies**: `cookie.partyKing.user` (the host's profile id) and `cookie.partyKing.admin` (the admin account id) are signed with ASP.NET Data Protection in `CookieAuthorizationHandler`, so clients can't forge identities; `cookie.partyKing.auth` holds the host's Spotify access token. The `CustomCookie` scheme adds `NameIdentifier` (profile id) and `Authentication` (access token) claims; the `AdminCookie` scheme backs `[AuthorizeAdmin]`.
- **Admin accounts**: stored in `AdminAccounts` with PBKDF2 password hashes. When none exists, the first one is created at startup from `AdminSettings:Username` plus either `Password` (plain text, hashed on seeding) or `PasswordHash` (put them in the untracked `appsettings.Local.json`). Generate a hash with `dotnet run --project PartyKing.API -- hash-password` (run from `src/api`; it prompts for the password and prints the value, without starting the app). `POST /admin/login` is rate limited (10 per minute per IP).
- `PartyKing.Infrastructure.Spotify` wraps the Spotify Web API client (`ISpotifyIntegrationClient`: auth, search, track lookup). `PartyKing.Infrastructure.Youtube` wraps YouTube's oEmbed endpoint (`IYoutubeOEmbedClient`).

Frontend (`PartyKing.UI/src`):
- React 19, react-router (`router/routes.tsx`: `/` Home lists sessions with a code search, `/s/:sessionId` MusicQueue, the one page of a session: everyone sees `NowPlayingCard` (art, title, artist, paused badge) and the queue; the viewer's role from `useViewerRole` (`GET /sessions/{id}/me`) adds the host's `HostPlayer` (plays the music, so the host must keep the page open; a YouTube player error skips to the next song, but only until 2 videos fail in a row; then it stops and shows a message until the host presses "Next song" or a song plays; `ErrorLog` lists errors for the admin only) or, for an admin who is not the host, `AdminPlayerControls` (Play/Pause and Next through the admin endpoints, disabled while no player is connected). A host whose login expired uses "I'm the host" in `SessionHeader` to log in again, `/callback` Spotify redirect, `/admin` admin login and session panel, plus Slideshow and ImageUploader), MUI + Tailwind v4, axios + SWR.
- The API base URL is hardcoded in `src/envVariables.ts` (`http://127.0.0.1:4200`) and must match the API launch profile. To test from phones on the LAN, change both `envVariables.ts` and `src/api/PartyKing.API/Properties/launchSettings.json` to the machine's LAN IP, but don't commit that change. Axios is configured in `src/service/request.ts`, and the `@/` import alias maps to `src/`.

## Conventions
- Commits follow the Conventional Commits format.
- Use subagent Explore for codebase exploration.
- Don't commit changes until explicitly stated.
- Stick to SOLID.
- Commit tests in a separate commit.
- Commits should be atomic.