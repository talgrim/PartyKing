# Deployment (Docker / Fly.io)

The upload hardening (file rules, limits, rate limit) is in `features/slideshow.md`; this file covers the image, nginx and Fly.

## Image

- `Dockerfile` (repo root) builds one image: nginx (`docker/nginx.conf`) serves the UI build and proxies `/api/*` to Kestrel on `127.0.0.1:5000` (prefix stripped); `docker/entrypoint.sh` runs both. `DOTNET_ThreadPool_MinThreads=32` is set there (see `architecture/persistence-and-concurrency.md`).
- The image build restores the identity client packages from GitHub Packages, so it needs a token as the build secret `nuget_token` (see "NuGet packages" below).
- The image ships no photos; `GET /sessions/{id}/photos/current` returns 204 when the session has no photos.
- The UI's `apiUrl` is `/api` in production builds and `http://127.0.0.1:4200` in dev (`VITE_API_URL` overrides). CORS exists in Development and the e2e environment only (`IsDevelopmentOrE2e`); production is same-origin.

## Fly.io

- `fly.toml` targets `party.talgrim.dev`: one always-on machine (SQLite and in-memory singletons), and the `/data` volume that holds `PartyKing.db` (`PARTYKING_DATABASE_PATH`), Data Protection keys (`DataProtection__KeysDirectory`) and uploaded photos (`ASPNETCORE_WEBROOT`).
- Secrets come from `fly secrets`: `SpotifySettings__*`, `Identity__WebhookSecret` (the HMAC secret shared with the identity service, see below), `LlmSettings__ApiKey`, `MailSettings__ApiKey`, `DataProtection__Certificate*`.
- Mail: a guest's request to delete a session mails the operator through Resend (`PartyKing.Infrastructure.Mail`, one `POST https://api.resend.com/emails`). `MailSettings__Sender`, `__Recipient` and `__ReplyTo` are in `fly.toml` `[env]` (not secret); the sender must be an address on the domain verified at Resend (`talgrim.dev`: DKIM, SPF and MX records on the `send` host in Namecheap, the root's mail forwarding is untouched). The key is created at Resend with "Sending access" for that domain and set with `fly secrets set MailSettings__ApiKey=...`. The Resend client is registered only when both the key and the sender are set; otherwise `NullMailClient` logs a warning and sends nothing, which is the case locally, in the e2e environment and in the demo. A mail that fails is logged (`SessionDeletionMailHandler failed on SessionDeletionScheduled`, with the status and Resend's reason) and the deletion stays scheduled.
- `SessionDeletionSettings__DelayDays` (7, a fraction is allowed) and `__SweepIntervalSeconds` (3600) are the delay of a requested deletion and how often the sweep runs; the defaults are in `appsettings.json`.
- Identity: `Identity__BaseUrl` (`https://auth.talgrim.dev/`) and `Identity__CookieName` (`__Secure-talgrim.session`) are in `[env]`. PartyKing keeps no accounts; the identity service's session cookie is `Domain=.talgrim.dev`, so the browser sends it to `party.talgrim.dev` and the API forwards it to the identity service (`features/sessions-and-auth.md`). Its events (an account was deleted) arrive as a signed `POST https://party.talgrim.dev/api/internal/events` (the identity service's `Webhooks:Subscribers:0:Url` is `https://party.talgrim.dev/api`); the signature is checked with `Identity__WebhookSecret`, which must be the same value as that subscriber's `Secret` in the identity service. Without it every event is refused (the playlists of a deleted account are then kept, and the service keeps retrying).
- **Cutover from the old accounts** (the migration `MovedAccountsToIdentityService` drops the `Accounts` table and the foreign key to it, so it cannot be undone from the app): import the accounts into the identity service with the same ids (the saved playlists point at them) and seed the admin there; stop registrations on the old build and snapshot `/data/PartyKing.db` (`fly ssh console` or a volume snapshot); set `Identity__WebhookSecret` with `fly secrets set` and, in the identity service, register PartyKing as a webhook subscriber with `Webhooks:Subscribers:0:Url` = `https://party.talgrim.dev/api` and `Webhooks:Subscribers:0:Secret` = the same value (it posts to `{Url}/internal/events`); deploy; check that `/health` is up, that logging in at `auth.talgrim.dev` signs you in on `party.talgrim.dev` (the account menu shows the username, `/admin` shows the panel for the admin), that a saved playlist of an imported user is still listed, and that deleting a test account removes its playlists (the identity service logs the delivery of `account.deleted`). Rollback: deploy the previous image and restore the snapshot of the database (the old build needs the `Accounts` table back).
- `/health` is served on the internal port 8182, which Fly's check uses, and answers 404 on the public port. `/metrics` is exposed only on the internal port 8183, which Fly scrapes (`[metrics]` in `fly.toml`).
- See the README section "Wdrożenie".

## Demo app

`fly.demo.toml` is the demo (`demo.party.talgrim.dev`, see `features/demo.md`): the same image and the same code, with `DemoSettings__Enabled=true` as a runtime setting. It is a separate Fly app (`partyking-demo`), so it shares no machine, volume, database or secret with production, and a visitor probing or flooding it cannot reach production: its UI calls its own `/api`, and production has the demo off (`/demo/sessions` answers 404 there).

- No secrets: no Spotify, identity (`Identity__Enabled='false'`: the demo has no accounts), LLM or mail key (so no mail is sent; scheduled deletion is refused in the demo anyway). `DataProtection__KeysDirectory` is emptied in `[env]`, because the image points it at `/data/keys`, which would need a certificate; the demo keeps its keys in memory (never the production ones; a restart only ends the demos that were running).
- `auto_stop_machines = 'stop'`, `min_machines_running = 0`: it scales to zero when idle and starts on the next visit (a few seconds). The volume `partyking_demo_data` holds the database and the uploaded photos; expired sessions are deleted with their photos, so a small volume is enough (`MaxSessions` x `MaxPhotosPerSession` x `MaxPhotoBytes` is the ceiling, 2.5 GB at the defaults).
- `deploy.yml` has a second job, `deploy-demo`, after the same tests, using the `FLY_API_TOKEN_DEMO` secret (a deploy token of the demo app).
- One-time setup, by hand: `fly apps create partyking-demo`, `fly volumes create partyking_demo_data --region fra --size 3 -a partyking-demo`, `fly certs add demo.party.talgrim.dev -a partyking-demo` with the DNS record it asks for, and the repository secret `FLY_API_TOKEN_DEMO` (`fly tokens create deploy -a partyking-demo`).
- Limits (sessions, starts per hour and IP, photos) are `DemoSettings__*` variables in `[env]` when the defaults do not fit.

## nginx

- Trusts `Fly-Client-IP` / `X-Forwarded-Proto` only when `TRUST_FLY_CLIENT_IP=1` (set in `fly.toml`).
- Adds a sandbox CSP on `/api/Images/` and raises the body limit to 100 MB for `/api/sessions/<guid>/photos/`; `proxy_buffering` is off in the `/photos/` location so the zip archive streams.

## Data Protection keys

The keys on `/data/keys` are encrypted with an X.509 certificate (`ProtectKeysWithCertificate`, from the `DataProtection__CertificateBase64` PFX plus the optional `DataProtection__CertificatePassword` secret). The API refuses to start when `DataProtection__KeysDirectory` is set without the certificate. Keys written before encryption stay readable.

## NuGet packages

The identity client (`Talgrim.Identity.Contract`, `.Client`, `.Client.AspNetCore`) is built in the `talgrim/auth` repo and published to GitHub Packages (`https://nuget.pkg.github.com/talgrim/index.json`): a preview (`x.y.z-preview.N`) on every merge to its `develop`, the release (`x.y.z`, tagged `vx.y.z`) on a merge to its `master`. `PartyKing.API.csproj` references `Talgrim.Identity.Client.AspNetCore` by version; to update, change that version. `src/api/nuget.config` names the source and maps only `Talgrim.*` to it (nobody can put a package of that name on nuget.org and have it picked up). It has **no credentials on purpose**: a credential in that file would override the one a developer has in their own NuGet config.

GitHub Packages wants a token even to read. Where it comes from:

- **A developer machine**: a classic personal access token with `read:packages` (a fine-grained token cannot read the NuGet registry), put once in the user-level NuGet config: `dotnet nuget add source https://nuget.pkg.github.com/talgrim/index.json --name github-talgrim --username talgrim --password <token> --store-password-in-clear-text`. The name has to be `github-talgrim`, the key `nuget.config` uses. Rider on Windows has its own user config (`%AppData%\NuGet\NuGet.Config`): run it there too (without the flag, Windows encrypts the password), and restart Rider.
- **CI**: `GITHUB_TOKEN` (the workflows ask for `packages: read`), handed to the `dotnet test` and `npm run e2e` steps as NuGet's own credential variable, `NuGetPackageSourceCredentials_github-talgrim` = `Username=talgrim;Password=<token>`. One-time setup: each of the three packages (the profile's Packages tab, then Package settings, Manage Actions access) has to give this repository Read access.
- **The image build on Fly**: the build runs on Fly's remote builder, so the token is a BuildKit secret, `flyctl deploy --remote-only --build-secret nuget_token=<token>`, which the Dockerfile reads for the one `dotnet publish` and passes as the same variable; it is never written to a file or a layer. `deploy.yml` passes `GITHUB_TOKEN`; by hand pass your token, for example `--build-secret nuget_token=$GH_PACKAGES_TOKEN`. `fly secrets` are runtime only and do not reach the build. `flyctl deploy --build-only --build-secret ...` tries the build without deploying.

A restore that fails with 401 has no token for the source (the variable, or the user-level source, is missing); 403 means the token cannot read the package (no `read:packages`, or the repository has no access to it).

## CI and deploy

`ci.yml` runs on PRs (backend tests, lint, frontend tests, build, and the `e2e` job, which also runs the demo suite); `deploy.yml` reuses `ci.yml` and deploys production and the demo (`deploy-demo`) on push to `master`, so a failing job blocks both. The backend and e2e jobs and both deploy jobs read the identity packages with `GITHUB_TOKEN` (see "NuGet packages").
