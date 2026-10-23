# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

PartyKing is a party app: guests upload photos that the API serves as a timed slideshow, plus a (currently mocked) music queue for YouTube/Spotify. It has a .NET 9 backend (`PartyKing.sln`) orchestrated by .NET Aspire, and a React + Vite frontend in `PartyKing.UI`. (`PartyKing.UI/README.md` is stale Next.js boilerplate; the UI is Vite.)

## Commands

Backend (run from repo root):
- Build: `dotnet build PartyKing.sln`
- Run everything (API + UI via Aspire): `dotnet run --project PartyKing.AppHost`
- Run the API only: `dotnet run --project PartyKing.API` (the `http` launch profile binds to `http://localhost:4200`)
- Add an EF migration: `dotnet ef migrations add <Name> --project PartyKing.Persistence --startup-project PartyKing.Persistence --context WriterDbContext`
- Migrations are applied automatically at API startup (`MigrateDb()` in `Program.cs`). The SQLite file is `PartyKing.db`, relative to the working directory.
- `PartyKing.API/PartyKing.http` has sample requests.

Frontend (from `PartyKing.UI`):
- `npm install`, then `npm start` (Vite dev server on port 2137, host `0.0.0.0`, polling watch for WSL2/Windows HMR)
- `npm run build`
- `npm test` runs `vitest run`. To run a single test: `npx vitest run src/path/to/file.test.ts`. Note that `vite.config.ts` references `testGlobalSetup.ts`/`testSetup.ts`, which don't exist yet.

Backend tests: `dotnet test src/api/PartyKing.Application.Tests` (xunit; currently covers `SongInputParser`).

## Architecture

Project dependencies (layered, top → bottom):
- **AppHost**: Aspire host. Starts `PartyKing.API` and the UI as an npm app on port 2137.
- **API**: controllers plus `Program.cs`. CORS allows any origin with credentials. Static files from `wwwroot` serve the images (`wwwroot/SampleImages/{Uploaded,Placeholder}`).
- **Application**: services (`SlideshowService`), options binding (`SlideshowConfiguration` ← `SlideshowSettings` section of appsettings), and DTO mappings. References Contract and Infrastructure.
- **Contract**: public DTOs under `V1/`, returned by controllers.
- **Infrastructure**: repositories that wrap EF Core. References Persistence and Domain.
- **Persistence**: EF Core SQLite `DbContext`s, entity configurations, migrations, seed data.
- **Domain**: entities (`SlideshowImage`, `SlideshowSettings`), enums, read/write models.
- **ServiceDefaults**: Aspire OpenTelemetry/health/resilience defaults (`AddServiceDefaults`, `MapDefaultEndpoints`).

Each layer registers its own services through a `DependencyInjection.cs` extension (`RegisterApplication`, `RegisterInfrastructure`), and `PartyKing.API/DependencyInjection.cs` chains them.

Key patterns and gotchas:
- **Reader/Writer DbContexts**: `ReaderDbContext` and `WriterDbContext` both derive from `PartyKingDbContextBase`, which hardcodes `UseSqlite("Data Source=PartyKing.db")` in `OnConfiguring`. They're registered with `AddDbContextFactory`. Repositories are **singletons** and create short-lived contexts through the factories. Migrations target `WriterDbContext`, and the snapshot file is `WriterDbContextModelSnapshot.cs`.
- **Stateful singletons**: `SlideshowService` and `SlideshowImagesRepository` are singletons that hold in-memory state: the current image and its expiration time, loaded placeholders, and current settings. `SlideshowController`'s constructor kicks off `InitializeAsync` without awaiting it (fire-and-forget) if the repository isn't initialized yet. This scans the placeholder directory under `WebRootPath` and loads settings from the DB.
- **Slideshow flow**: `get-photo` returns the current image until `SlideTime` expires. It then advances to the next uploaded image from the DB, falling back to a random placeholder. When an image with `DeleteAfterPresentation` is rotated out, it gets deleted.
- **Settings live in the DB**: slide time and other settings are stored in the `SlideshowSettings` table (migration `MovedConfigurationToDatabase`) and edited through `get-configuration`/`update-configuration`. Directory paths still come from appsettings.
- **Errors**: `CoreController` maps `ErrorOr` errors to ProblemDetails (Validation→400, NotFound→404, Conflict→409).
- `QueueController` is a mock: it alternates between hardcoded YouTube URLs and a Spotify placeholder. `PartyKing.Infrastructure.Spotify` exists only as build output and is not in the solution.

Frontend (`PartyKing.UI/src`):
- React 19, react-router (`router/routes.tsx`: Home, Login, Callback, Slideshow, ImageUploader, Music), MUI + Tailwind v4, axios + SWR.
- The API base URL is hardcoded in `src/envVariables.ts` (`http://localhost:4200`) and must match the API launch profile. To test from phones on the LAN, change both `envVariables.ts` and `PartyKing.API/Properties/launchSettings.json` to the machine's LAN IP, but don't commit that change. Axios is configured in `src/service/request.ts`, and the `@/` import alias maps to `src/`.

## Conventions
- Stick to the Clean Architecture.
- Commits follow the Conventional Commits format.
- REST: Plural nouns
- Use subagent Explore for codebase exploration