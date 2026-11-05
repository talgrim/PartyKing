# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

**This file is an index.** Keep it short: commands, conventions, the layering rules and the map below. Details of a feature or a subsystem belong in `docs/` (see "Where the details are"); do not grow this file.

## Overview

PartyKing is a party app: guests upload photos that the API serves as a timed slideshow, plus music sessions: each session has its own queue (YouTube links and Spotify tracks, persisted in SQLite) and its own host, who plays it through the Spotify Web Playback SDK. It has a .NET 9 backend (`src/api/PartyKing.sln`) orchestrated by .NET Aspire, and a React + Vite frontend in `src/ui/PartyKing.UI`.

## Conventions

- Commits follow the Conventional Commits format.
- Use subagent Explore for codebase exploration.
- Don't commit changes until explicitly stated.
- Stick to SOLID.
- Commit tests in a separate commit.
- Commits should be atomic if possible. If working on a big task and commiting parts of the code in batches, separate commits may not build intermittently as long as the project builds after all changes are commited (no need to re-apply changes only to adhere to this convention).
- When exploring automatically assume that the user is in "Interview" mode, meaning that you can ask him questions regarding the task. Also don't make any changes when prompt ask only for exploring and analysis.
- All classes placed in the Contract project should end with "Dto", if they only specify a dto
- Don't use expression statements for methods if they are long enough to be wrapped to the next line.
- Put details in `docs/`, not here. When a change needs more than a line or two of explanation (a new rule, a gotcha, a flow), add it to the matching file in the table below, or create one and add a row and a path-scoped rule in `.claude/rules/`. Keep `CLAUDE.md` an index.
- Naming and code-style patterns of the backend are in `.claude/rules/api-naming.md`.

## Commands

Backend (run from `src/api`):
- Build: `dotnet build PartyKing.sln`
- Run everything (API + UI via Aspire): `dotnet run --project PartyKing.AppHost`
- Run the API only: `dotnet run --project PartyKing.API` (the `http` launch profile binds to `http://127.0.0.1:4200`)
- Add an EF migration: `dotnet ef migrations add <Name> --project PartyKing.Persistence --startup-project PartyKing.Persistence --context WriterDbContext`
- Migrations are applied automatically at API startup (`MigrateDb()` in `Program.cs`). The SQLite file is `PartyKing.db` relative to the working directory, or `PARTYKING_DATABASE_PATH` when set.
- Tests: `dotnet test PartyKing.sln` - takes ~7-8s. Four projects: `PartyKing.Domain.Tests` (references only the Domain: aggregates, events, rules; no fakes, no database), `PartyKing.Application.Tests` (services, repositories on SQLite, clients), `PartyKing.Api.Tests` (controllers, filters, notifiers) and `PartyKing.Architecture.Tests` (the layering below, read from the assemblies' references)

Frontend (from `src/ui/PartyKing.UI`):
- `npm install`, then `npm start` (Vite dev server on port 2137, host `0.0.0.0`, polling watch for WSL2/Windows HMR)
- `npm run build`
- `npm run lint` runs ESLint (flat config in `eslint.config.js`: `typescript-eslint` recommended, react-hooks, react-refresh, import and unused-imports rules; formatting is not enforced). `npx eslint --fix` removes unused imports.
- `npm test` runs `vitest run`, takes ~15s. To run a single test: `npx vitest run src/path/to/file.test.ts`. Tests run in jsdom with `testSetup.ts` (jest-dom matchers).
- E2E tests (Playwright, Chromium): `npm run e2e` from `src/ui/PartyKing.UI`, takes ~3 min. First time only: `npx playwright install chromium`. It starts its own API and Vite, so stop your dev servers first (ports 4200 and 2137). Details and the spec inventory: `docs/testing-e2e.md`.

## Architecture

Layered, top to bottom: **AppHost** (Aspire) → **API** (controllers, hubs, cookies, `Program.cs`; the composition root) → **Application** (services, ports, event handlers, mappings) → **Domain** (aggregates, rules, events, repository ports). **Infrastructure** (`.Spotify`, `.Youtube`, `.Lyrics`, `.Llm`) implements the ports; **Persistence** is the EF Core SQLite layer; **Contract** holds the public DTOs (`V1/`); **ServiceDefaults** is Aspire telemetry. Each layer registers its services in its own `DependencyInjection.cs`.

**Layering** (checked by `LayerDependencyTests`): Domain references no `PartyKing`/`Microsoft` assembly and not `System.Text.Json`; Contract no `PartyKing` assembly; Application only Domain and Contract and no web assembly (Data Protection is allowed, `JoinTokenProtector` uses it); Persistence only Domain; no Infrastructure project sees the web, the API or another Infrastructure project, and only `PartyKing.Infrastructure` uses Persistence.

**Application never sees HTTP** (no `HttpContext`, `IFormFile`, cookies or MVC; `PartyKing.Architecture.Tests` fails CI otherwise): services return what happened and the API writes the cookies. Application never sees the Spotify SDK either.

Rules that bite:
- Repositories are singletons that create short-lived `DbContext`s from factories; never do slow work in the constructor of a scoped service.
- Game rules live in the Domain aggregates, not in repositories or services; a game action is a Domain method returning an outcome, run through `UpdateAsync` under the session write lock.
- Pushes are payload-free and sent by domain event handlers, not by services; no event or push carries a secret.
- Per-caller views that hide secrets (`ResistanceGameViewBuilder`, `RingoService.ToDto`, lobby DTOs) are hand-written on purpose; other mappings use Riok.Mapperly.
- Error codes and messages are matched by the UI and tests; keep them stable. Only texts the server generates are localized (`UiLanguage`).

## Where the details are

Path-scoped rules in `.claude/rules/` remind you to read the matching file when you touch those paths; read it before changing the area.

| Topic | File |
|---|---|
| Projects, Application services, mappings, DI | `docs/architecture/layers.md` |
| DbContexts, SQLite, write locks, metrics, errors | `docs/architecture/persistence-and-concurrency.md` |
| Domain events, dispatch, SignalR topics and notifiers | `docs/architecture/domain-events-and-realtime.md` |
| Sessions, password, QR join, host login, cookies, admin, dev personas | `docs/features/sessions-and-auth.md` |
| Queue, player, fallback playlist, lyrics | `docs/features/music.md` |
| Slideshow, photo upload, archive | `docs/features/slideshow.md` |
| Games hub, lobby layer, Ringo, Never have I ever, LLM | `docs/games/games-lobby.md` |
| Resistance implementation (presence, seats, phases, plot cards, views) | `docs/games/resistance-implementation.md` |
| Resistance rules and expansions (Polish) | `docs/games/resistance-rules.md` |
| Frontend structure, polling, i18n | `docs/frontend.md` |
| E2E suite | `docs/testing-e2e.md` |
| Docker, Fly.io, nginx, secrets | `docs/deployment.md` |
| Grafana dashboards | `docs/grafana/` |

## CI

`.github/workflows/ci.yml` (PRs) runs `dotnet test PartyKing.sln` (all backend tests), `npm run lint`, `npm test` (all frontend tests) and `npm run build`. `deploy.yml` runs `flyctl deploy --remote-only` on push to `master`. `/health`, `/alive` and `/metrics` are mapped in every environment (`MapDefaultEndpoints`); nginx keeps them off the public port.
