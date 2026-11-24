# Shared login: PartyKing becomes a consumer of auth.talgrim.dev

Status: **implemented on `feat/shared-login`**, not yet merged or deployed. Already done on the identity side: the accounts are imported, the admin is
seeded and the integration events (a transactional outbox delivered as signed webhooks) are merged into the identity
service's `develop`. The accounts (users and admins) move out of PartyKing into a separate identity
service, `auth.talgrim.dev` (repo `talgrim/auth`, its `plan.md` holds the service side and the API contract). PartyKing
keeps only what it needs: asking the identity service who the caller is. The same login then works on `talgrim.dev`,
`wip.talgrim.dev` and `notes.talgrim.dev`.

Why this is done in a separate service and not by widening PartyKing's own cookie: PartyKing is a party app with
guest-facing endpoints, a public demo, OAuth and third-party scripts, and it is deployed on every push to `master`
(one machine, one volume, a short outage per deploy). The login of every other site, and private notes, should not
depend on it.

## Decisions

- **Cookie:** the identity service sets one opaque session cookie `__Secure-talgrim.session` with
  `Domain=.talgrim.dev; Secure; HttpOnly; SameSite=Lax`. PartyKing never reads or writes it (the browser sends it to
  `party.talgrim.dev` on its own); it forwards it to the identity service.
- **Validation:** introspection. `IIdentityClient` calls `GET https://auth.talgrim.dev/api/session[?role=Admin]` with the
  `Cookie` header and caches the answer in memory for about 30 s (positive and negative), keyed by a hash of the cookie
  value. No Data Protection key is shared.
- **Anonymous guests cost nothing:** a request with no session cookie never calls the identity service. Guests at a party
  have no account, so the polled session endpoints stay as cheap as today.
- **Outage behaviour:** when the identity service cannot be reached, answer 503, not 401 (the UI reads 401 as "logged out"
  and would drop the hint), and keep using a cached answer for a cookie that was valid a moment ago.
- **Login UI moves out:** login, registration, account settings and the admin users page live on `auth.talgrim.dev`.
  In PartyKing, "Log in" and "Sign up" are links to `https://auth.talgrim.dev/login?next=<this page>`.
  The UI asks `GET https://auth.talgrim.dev/api/me` (cross origin, with credentials) for who is signed in.
- **Demo has no accounts:** `demo.party.talgrim.dev` runs with `Identity:Enabled=false`, which registers a client that
  always answers "nobody". It does not call the identity service and the account menu stays hidden.
- **Ids do not change:** `SavedPlaylist.OwnerAccountId` keeps pointing at the same account ids, which the import keeps.
- **Cascade becomes an event:** the database foreign key `SavedPlaylists -> Accounts` (cascade delete, in
  `SavedPlaylistConfiguration`) goes away. When an account is deleted the identity service queues an `account.deleted`
  integration event in an outbox (in the same transaction as the delete) and delivers it with a signed
  `POST /api/internal/events`, retrying until PartyKing answers 2xx, also across its own restarts. PartyKing deletes that
  owner's saved playlists. There is no broker: it is plain HTTP, and the identity plan's section "Integration events
  (webhooks)" has the contract. The identity service no longer sends the earlier `DELETE /internal/accounts/{id}` call,
  so anything on `feat/shared-login` that still implements that call has to be switched to the events endpoint (step 2.5)
  before the cutover.

## What moves out of PartyKing

Everything here is copied into the identity repo first (its `plan.md`, section "Move the account code out of PartyKing"),
then removed here.

| Area | Files (under `src/api` unless noted) |
|---|---|
| Domain | `PartyKing.Domain/Entities/Auth/Account.cs`, `AccountRole.cs`, the username and password rules, `Repositories/IAccountsRepository.cs` |
| Application | `PartyKing.Application/Accounts/` (`AccountService`, `AccountAdminService`, `AdminSeeder`, `AccountIdentity`), the registrations in `PartyKing.Application/DependencyInjection.cs` |
| Infrastructure and persistence | `AccountsRepository`, `MeteredAccountsRepository`, `AccountConfiguration`, the `Accounts` DbSet in `PartyKingDbContext`, the registrations in `PartyKing.Infrastructure/DependencyInjection.cs` |
| API | `AccountController`, the `/admin/users` endpoints of `AdminController`, `PasswordHashTool`, the account methods of `ISignedCookies` / `SignedCookies` (`GrantAccount`, `RevokeAccount`, `TryReadAccount`), `CookieNames.Account`, `AccountCookieAuthenticationHandler`, the `AdminSettings` seeding config |
| UI (`src/ui/PartyKing.UI/src`) | `components/Account/*` (`AccountDialog`, `CredentialsForm`), `pages/Admin/AdminUsers.tsx`, the login form part of `pages/Admin/Admin.tsx`, `service/endpoints/account/accountApi.ts` (the calls go to the identity service instead) |
| Tests | `AccountTests`, `AccountPasswordTests`, `AccountServiceTests`, `AccountAdminServiceTests`, `AdminSeederTests`, `AccountsRepositoryTests`, `MeteredRepositoryTests` (the account part), `AccountAuthenticationTests`, the account part of `SignedCookiesTests`, e2e `accounts.spec.ts` and `admin-users.spec.ts` |

What stays: `[AuthorizeAccount]` and `[AuthorizeAdmin]` (same names, same meaning), `HttpContext.ReadSessionCallerAsync`
and `SessionCaller.IsAdmin`, `AccountPlaylistsController` (`/account/saved-playlists`), `SavedPlaylistService`, the
`AccountRole` contract enum, the host cookies (`user`, `auth`), session unlocks and game cookies. They are unrelated to
who is logged in and stay host-only.

## Steps

### 1. Identity client (Application port, Infrastructure adapter)
1. Port `IIdentityClient` in `PartyKing.Application` (`GetCallerAsync(cookieHeader, requiredRole, ct)` returning
   `IdentityResult`: `Anonymous`, `Account(id, username, role)`, `Forbidden`, `Unavailable`). Domain stays free of HTTP.
2. HTTP adapter in `PartyKing.Infrastructure` (typed `HttpClient`, 3 s timeout, `IMemoryCache` with a ~30 s entry, a
   short negative entry, a size limit on the cache). Settings: `Identity:BaseUrl`, `Identity:CookieName`,
   `Identity:Enabled`. Metrics: cache hits, calls, failures (next to `AuthorizationMetrics`).
3. `NullIdentityClient` for `Identity:Enabled=false` (the demo) and a fake for tests.

### 2. Authentication in the API
1. Replace `AccountCookieAuthenticationHandler` with a handler built on `IIdentityClient`: no session cookie means no
   result and no call; `Unavailable` means a 503 response, not an authentication failure.
2. `[AuthorizeAccount]`, `[AuthorizeAdmin]`, `ReadSessionCallerAsync` and `SessionCaller.IsAdmin` keep their contract.
   Check each of the ~20 controllers that read the caller (games, sessions, slideshow, queue, player, lyrics, demo,
   hub): an account that the identity service says is `Admin` is an admin; a `User` is not.
3. Replace the per-request database check. The old code skipped the database for a `User` cookie on polled endpoints,
   because the role was signed into the cookie. The same effect now comes from the cache.
4. Delete the moved account code listed above.
5. Events endpoint `POST /internal/events` (so `/api/internal/events` behind nginx). It reads the raw body, verifies
   `X-Identity-Signature` (`sha256=<hex>` of HMAC-SHA256 over `"{timestamp}.{body}"` with a shared secret,
   `Identity:WebhookSecret`, Fly secret) in constant time, and rejects a timestamp more than 5 minutes off. The body is
   the envelope `{id, type, occurredAt, data}`. For `account.deleted` it calls `ISavedPlaylistService` to delete the
   playlists of owner `data.accountId`; a type it does not know is ignored. It answers 204 once handled, also when there
   was nothing to delete (any other status makes the identity service retry, and a 404 no longer counts as delivered).
   Deleting is idempotent, so a repeated `id` is harmless. The signature covers the body, not the path, so nginx
   stripping `/api` needs no path prefix setting (`Identity:WebhookPathPrefix` goes away). Rate limit it and keep it out
   of Swagger.
6. `DevPersonaMiddleware`: it currently hides the account cookie from a persona by name; it must hide
   `__Secure-talgrim.session` (or the configured name) instead.

### 3. Database
1. One EF migration (the generated one, not edited by hand) that drops the foreign key from
   `SavedPlaylists.OwnerAccountId` to `Accounts` and drops the `Accounts` table. The column and its index stay. There is
   no second release: the accounts are already in the identity service.
2. Take a Fly volume snapshot right before the deploy: `fly volumes snapshots create <volume-id> -a partyking`.
   It is the only way back (see the runbook).

### 4. UI
1. `useAccount` asks `GET {identityUrl}/api/me` with credentials. A 401 still means "nobody signed in" and clears the
   localStorage hint. `alwaysCheck` stays for the admin pages. Add the identity URL to `envVariables.ts`
   (`VITE_IDENTITY_URL`, default `https://auth.talgrim.dev`, `http://127.0.0.1:5173` in dev).
2. `AccountMenu`: for a visitor, "Log in" and "Sign up" become links to
   `{identityUrl}/login?next=<current URL>` and `{identityUrl}/register?next=<current URL>`. Signed in: the username,
   "Account settings" (link to the identity service) and "Log out" (`POST {identityUrl}/api/logout`, then the hint is cleared).
3. `/admin` shows a link to the login when nobody is signed in, the "not an admin account" notice for a user and the
   panel for an admin, as today. The users list and the reset-password dialog move to the identity service; the admin
   panel can link to `{identityUrl}/admin/users`.
4. Keep Polish and English strings for what stays; remove the strings of the deleted dialogs.
5. The demo keeps hiding the account menu.

### 5. Configuration and deployment
1. `fly.toml`: `Identity__BaseUrl = 'https://auth.talgrim.dev'`, `Identity__CookieName = '__Secure-talgrim.session'`.
   Secret: `Identity__WebhookSecret`. Remove `AdminSettings__*` (the admin is already seeded in the identity service).
2. `fly.demo.toml`: `Identity__Enabled = 'false'`.
3. nginx needs no change for the cookie (it is passed through). The events path is public but HMAC-protected.
4. Local development: run the identity service on `http://127.0.0.1:5051` (API) and `5173` (UI). A cookie with no `Domain` on `127.0.0.1` is
   shared by every port, so the UI (2137), the API (4200) and the identity service (5051, 5173) all see it. The identity
   service's `?next=` allowlist must include `http://127.0.0.1:2137` in Development. Decide whether the Aspire
   `PartyKing.AppHost` starts the identity project (an external project reference) or only documents a manual start.
5. `appsettings.Development.json` and `appsettings.e2e.json` point at the local identity service or the fake.

### 6. Tests (committed separately from the code)
- Application: `IdentityClient` (cache hit, expiry, negative cache, unavailable keeps the last good answer, no cookie means
  no call), the event handling. Api: the new authentication handler against a fake `IIdentityClient`
  (admin, user, anonymous, forbidden, unavailable = 503), the events endpoint (valid signature, wrong signature, a body
  changed after signing, old timestamp, unknown event type ignored, `account.deleted` for an unknown account answers 204), `AccountAuthenticationTests` rewritten against the fake, `DevPersonaMiddlewareTests` with
  the new cookie name.
- Architecture tests still pass (Domain has no HTTP, Application only depends on the port).
- E2E: the specs that create users or admins (`admin-session.spec.ts`, `saved-playlist-fallback.spec.ts`,
  `photos-admin.spec.ts` and any that call `registerUser`) need an identity. Decided: a small Node stand-in started by
  Playwright (`e2e/fixtures/fake-identity`): in-memory accounts, the same `/api/{register,login,logout,me,session}`
  contract, a seeded admin, and a cookie with no `Domain`. Replace `fixtures/accounts` accordingly. The real identity
  service is not started in CI.
- Remove `accounts.spec.ts` and `admin-users.spec.ts` (they move to the identity repo).

### 7. Docs (same change as the code)
- `docs/features/sessions-and-auth.md`: rewrite "Cookies" and "Accounts" (accounts now live in the identity service),
  keep sessions, host login and session unlocks, add "Identity client and events".
- `docs/deployment.md`: new settings and secrets, the cutover runbook, the migration that drops `Accounts`.
- `docs/frontend.md`: `useAccount`, the identity URL, the login links.
- `docs/testing-e2e.md`: the fake identity.
- `CLAUDE.md` row for sessions and auth mentions the identity service; `.claude/rules/sessions-auth.md` paths drop the
  deleted folders.

## Cutover runbook (with the identity service)

Already done: the identity service is deployed, the accounts are imported and the admin is seeded.

1. Accounts registered in PartyKing after the import would be lost with the table. Either re-run `import-accounts`
   (idempotent) right before the deploy, or return a temporary 503 on `POST /account/register` from the last import
   until the deploy.
2. Take a snapshot of the PartyKing volume.
3. Deploy PartyKing. Everyone is logged out once (a different cookie).
4. Check: login at `auth.talgrim.dev`, admin page on `party.talgrim.dev`, saved playlists of an existing user still listed,
   a deleted test account removes its playlists through the `account.deleted` event (the identity service logs
   "Told party.talgrim.dev of account.deleted").
5. **Rollback:** the `Accounts` table is gone, so the previous image cannot simply be redeployed (its model expects the
   table). Restore the volume from the snapshot, then redeploy the previous image. Everything written to PartyKing after
   the snapshot is lost, and accounts and password changes made after the cutover exist only in the identity service.

## Risks

- Biggest change in the whole effort: many controllers and tests touch the caller. Do it on a branch against `develop` and
  run the full suite and the e2e before the cutover.
- The identity service is now a dependency of every logged-in feature (admin, saved playlists). Cache and 503, never a
  logout. Anonymous guests are unaffected.
- A wider cookie scope: any `*.talgrim.dev` host sees the account cookie, so a compromise of any subdomain is a
  compromise of accounts, including the admin. Keep PartyKing's CSP and dependencies tidy.
- Admin sessions change from 12 h in PartyKing to whatever the identity service sets (keep 12 h there).
- Dropping `Accounts` in the same release means there is no cheap rollback: only a volume snapshot restore.
- Whether `GET /sessions` and other public endpoints should ever need the identity service: no.
