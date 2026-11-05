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

## nginx

- Trusts `Fly-Client-IP` / `X-Forwarded-Proto` only when `TRUST_FLY_CLIENT_IP=1` (set in `fly.toml`).
- Adds a sandbox CSP on `/api/Images/` and raises the body limit to 100 MB for `/api/sessions/<guid>/photos/`; `proxy_buffering` is off in the `/photos/` location so the zip archive streams.

## Data Protection keys

The keys on `/data/keys` are encrypted with an X.509 certificate (`ProtectKeysWithCertificate`, from the `DataProtection__CertificateBase64` PFX plus the optional `DataProtection__CertificatePassword` secret). The API refuses to start when `DataProtection__KeysDirectory` is set without the certificate. Keys written before encryption stay readable.

## CI and deploy

`ci.yml` runs on PRs (backend tests, lint, frontend tests, build, and the `e2e` job); `deploy.yml` reuses `ci.yml` and runs `flyctl deploy --remote-only` on push to `master`, so a failing job blocks a deploy.
