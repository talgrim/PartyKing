# Persistence, concurrency, metrics and errors

## DbContexts

- `ReaderDbContext` and `WriterDbContext` both derive from `PartyKingDbContextBase`, which sets `UseSqlite("Data Source=PartyKing.db")` (or `PARTYKING_DATABASE_PATH`) in `OnConfiguring` only when the options are not configured already. A test can pass its own connection string through `AddDbContextFactory<WriterDbContext>(o => o.UseSqlite(...))`.
- Both are registered with `AddDbContextFactory`. Repositories are **singletons** and create short-lived contexts through the factories.
- Migrations target `WriterDbContext`; the snapshot is `WriterDbContextModelSnapshot.cs`.

## SQLite waits

- The connection string has `Default Timeout=3`. The Microsoft.Data.Sqlite default of 30 s outlasts nginx's 15 s, and a waiting command holds a request thread, so a burst of waiters starved the thread pool and every request timed out as a 504.
- `DOTNET_ThreadPool_MinThreads=32` is set in the `Dockerfile`.

## Shared reads

- `LobbyGamesRepository.GetAsync` (Ringo and Resistance) shares one in-flight query per session between concurrent callers, because every `ResistanceChanged` push makes all clients fetch the game at once. Callers must treat the result as read-only.
- Every write goes through its `SaveAsync`, which drops the shared reads once the commit is done, so a read that started before a write is never handed to a caller who starts asking after the write returned (a caller overlapping the commit itself may still join the older read). A finished read removes only its own entry from the dictionary, never a newer read's.
- The write lock around game actions is described in `../games/games-lobby.md`.

## Metrics

- Meters: `PartyKing.API` (`ActionMetricsFilter` times every controller action; `AuthorizationMetrics` times cookie auth and session access), `PartyKing.Infrastructure` and `PartyKing.Infrastructure.Spotify` (`MeteredSpotifyIntegrationClient`). Durations are in milliseconds; the thread pool, request and repository durations are in the runtime and PartyKing meters.
- `PartyKing.Infrastructure`: `Metered*Repository` decorators registered through `AddMetered`; the queue advance lock wait; `partyking.games.write_lock.wait` (tags `repository`, `operation`, `acquired`), the time a Resistance or Ringo write waits for the session's write lock; and the counter `partyking.games.reads` (tags `repository`, `shared`: whether a game read joined the query already in flight).
- Grafana dashboards are in `docs/grafana`: `game-write-lock.json`, and `resistance-fetches.json`, which divides the Resistance GETs by the successful writes and shows Fly's CPU balance and CPU by mode on top, to tell throttling from load.
- `ServiceDefaults` exports the meters through OpenTelemetry and a Prometheus endpoint at `/metrics`; nginx exposes it only on the internal port 8183, which Fly scrapes (`[metrics]` in `fly.toml`).

## Errors

- `CoreController` maps `ErrorOr` errors to ProblemDetails: Validation→400, Unauthorized→401, NotFound→404, Conflict→409. Unhandled exceptions answer a 500 ProblemDetails.
- Error messages stay in English. Codes and messages are matched by the UI and tests, so keep them stable.
