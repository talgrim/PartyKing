# Deployment (Docker / Fly.io)

The upload hardening (file rules, limits, rate limit) is in `features/slideshow.md`; this file covers the image, nginx and Fly.

## Image

- `Dockerfile` (repo root) builds one image: nginx (`docker/nginx.conf`) serves the UI build and proxies `/api/*` to Kestrel on `127.0.0.1:5000` (prefix stripped); `docker/entrypoint.sh` runs both. `DOTNET_ThreadPool_MinThreads=32` is set there (see `architecture/persistence-and-concurrency.md`).
- The image ships no photos; `GET /sessions/{id}/photos/current` returns 204 when the session has no photos.
- The UI's `apiUrl` is `/api` in production builds and `http://127.0.0.1:4200` in dev (`VITE_API_URL` overrides). CORS exists in Development and the e2e environment only (`IsDevelopmentOrE2e`); production is same-origin.

## Fly.io

- `fly.toml` targets `party.talgrim.dev`: one always-on machine (SQLite and in-memory singletons), and the `/data` volume that holds `PartyKing.db` (`PARTYKING_DATABASE_PATH`), Data Protection keys (`DataProtection__KeysDirectory`) and uploaded photos (`ASPNETCORE_WEBROOT`).
- Secrets come from `fly secrets`: `SpotifySettings__*`, `AdminSettings__*`, `LlmSettings__ApiKey`, `DataProtection__Certificate*`.
- `/health` is served on the internal port 8182, which Fly's check uses, and answers 404 on the public port. `/metrics` is exposed only on the internal port 8183, which Fly scrapes (`[metrics]` in `fly.toml`).
- See the README section "Wdrożenie".

## Demo app

`fly.demo.toml` is the demo (`demo.party.talgrim.dev`, see `features/demo.md`): the same image and the same code, with `DemoSettings__Enabled=true` as a runtime setting. It is a separate Fly app (`partyking-demo`), so it shares no machine, volume, database or secret with production, and a visitor probing or flooding it cannot reach production: its UI calls its own `/api`, and production has the demo off (`/demo/sessions` answers 404 there).

- No secrets: no Spotify, admin account or LLM key. `DataProtection__KeysDirectory` is emptied in `[env]`, because the image points it at `/data/keys`, which would need a certificate; the demo keeps its keys in memory (never the production ones; a restart only ends the demos that were running).
- `auto_stop_machines = 'stop'`, `min_machines_running = 0`: it scales to zero when idle and starts on the next visit (a few seconds). The volume `partyking_demo_data` holds the database and the uploaded photos; expired sessions are deleted with their photos, so a small volume is enough (`MaxSessions` x `MaxPhotosPerSession` x `MaxPhotoBytes` is the ceiling, 2.5 GB at the defaults).
- `deploy.yml` has a second job, `deploy-demo`, after the same tests, using the `FLY_API_TOKEN_DEMO` secret (a deploy token of the demo app).
- One-time setup, by hand: `fly apps create partyking-demo`, `fly volumes create partyking_demo_data --region fra --size 3 -a partyking-demo`, `fly certs add demo.party.talgrim.dev -a partyking-demo` with the DNS record it asks for, and the repository secret `FLY_API_TOKEN_DEMO` (`fly tokens create deploy -a partyking-demo`).
- Limits (sessions, starts per hour and IP, photos) are `DemoSettings__*` variables in `[env]` when the defaults do not fit.

## nginx

- Trusts `Fly-Client-IP` / `X-Forwarded-Proto` only when `TRUST_FLY_CLIENT_IP=1` (set in `fly.toml`).
- Adds a sandbox CSP on `/api/Images/` and raises the body limit to 100 MB for `/api/sessions/<guid>/photos/`; `proxy_buffering` is off in the `/photos/` location so the zip archive streams.

## Data Protection keys

The keys on `/data/keys` are encrypted with an X.509 certificate (`ProtectKeysWithCertificate`, from the `DataProtection__CertificateBase64` PFX plus the optional `DataProtection__CertificatePassword` secret). The API refuses to start when `DataProtection__KeysDirectory` is set without the certificate. Keys written before encryption stay readable.

## CI and deploy

`ci.yml` runs on PRs (backend tests, lint, frontend tests, build, and the `e2e` job, which also runs the demo suite); `deploy.yml` reuses `ci.yml` and deploys production and the demo (`deploy-demo`) on push to `master`, so a failing job blocks both.
