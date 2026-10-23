# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

PartyKing is a party app: guests upload photos that the API serves as a timed slideshow, plus music sessions: each session has its own queue (YouTube links and Spotify tracks, persisted in SQLite) and its own host, who plays it through the Spotify Web Playback SDK. It has a .NET 9 backend (`src/api/PartyKing.sln`) orchestrated by .NET Aspire, and a React + Vite frontend in `src/ui/PartyKing.UI`. (`PartyKing.UI/README.md` is stale Next.js boilerplate; the UI is Vite.)

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
- **Music queue**: `QueueItem`s (YouTube or Spotify) live in the `QueueItems` table, managed by the singleton `QueueRepository`, which has no in-memory queue state except a per-session advance lock. Everything is scoped by session: guests add songs without logging in via `POST /sessions/{id}/songs` (body `{input}`: a Spotify track URL/URI or a YouTube URL, parsed by `SongInputParser`) and search Spotify with `GET /sessions/{id}/tracks?query=`. `GET /sessions/{id}/songs` lists pending items. `POST /sessions/{id}/songs/next` is host-only: `[AuthorizeByCookie]` plus a check that the cookie's profile id is the session's `HostProfileId` (403 otherwise). `GET /spotify/token` needs a host login but is per browser, not per session.
- **Session host**: one host per session, and one Spotify account can host one session (unique index on `Session.HostProfileId`). The host flow: `POST /sessions/{id}/host-login-urls` stores a short-lived `OAuthState` (the OAuth `state` parameter, single use) and returns the Spotify URL; Spotify redirects to the fixed `/callback`, which posts `{code, state}` to `POST /authenticate`; `SessionsRepository.TryClaimAsync` then atomically claims the session (first account wins, others get 409, the current host logging in again is fine). An admin can release the host (`DELETE /admin/sessions/{id}/host`). `HostTokenProvider` (singleton) caches and refreshes one access token per host profile, resolved through the session, and returns `Error.Unauthorized` when the refresh token is dead (the host logs in again). Spotify profile ids are strings, and `SpotifyProfile.DisplayName` is shown as "Current Host". The OAuth redirect URI uses `127.0.0.1`, so log in via `http://127.0.0.1:2137/...`, not `localhost`.
- **Cookies**: `cookie.partyKing.user` (the host's profile id) and `cookie.partyKing.admin` (the admin account id) are signed with ASP.NET Data Protection in `CookieAuthorizationHandler`, so clients can't forge identities; `cookie.partyKing.auth` holds the host's Spotify access token. The `CustomCookie` scheme adds `NameIdentifier` (profile id) and `Authentication` (access token) claims; the `AdminCookie` scheme backs `[AuthorizeAdmin]`.
- **Admin accounts**: stored in `AdminAccounts` with PBKDF2 password hashes. When none exists, the first one is created at startup from `AdminSettings:Username` plus either `Password` (plain text, hashed on seeding) or `PasswordHash` (put them in the untracked `appsettings.Local.json`). Generate a hash with `dotnet run --project PartyKing.API -- hash-password` (run from `src/api`; it prompts for the password and prints the value, without starting the app). `POST /admin/login` is rate limited (10 per minute per IP).
- `PartyKing.Infrastructure.Spotify` wraps the Spotify Web API client (`ISpotifyIntegrationClient`: auth, search, track lookup).

Frontend (`PartyKing.UI/src`):
- React 19, react-router (`router/routes.tsx`: `/` Home lists sessions with a code search, `/s/:sessionId` MusicQueue for guests, `/s/:sessionId/music` Music for the host player, `/callback` Spotify redirect, `/admin` admin login and session panel, plus Slideshow and ImageUploader), MUI + Tailwind v4, axios + SWR.
- The API base URL is hardcoded in `src/envVariables.ts` (`http://127.0.0.1:4200`) and must match the API launch profile. To test from phones on the LAN, change both `envVariables.ts` and `src/api/PartyKing.API/Properties/launchSettings.json` to the machine's LAN IP, but don't commit that change. Axios is configured in `src/service/request.ts`, and the `@/` import alias maps to `src/`.

## Conventions
- Commits follow the Conventional Commits format.
- Use subagent Explore for codebase exploration.
- Don't commit changes until explicitly stated.
- Stick to SOLID.
- Commit tests in a separate commit.
- Commits should be atomic.