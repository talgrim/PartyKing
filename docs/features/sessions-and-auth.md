# Sessions, host and authentication

## Contents

- Sessions
- Session password and QR join
- Session host (Spotify login)
- Cookies
- Accounts (users and admins)
- Dev personas
- Session aggregate, events and handlers

## Sessions

- A `Session` has a GUID id, a unique short `Code` (from `SessionCodeGenerator`), a name defaulting to "New session" and a nullable `HostProfileId`. It owns one `QueueItem` queue (`QueueItem.SessionId`, cascade delete) and its own slideshow.
- `HostDisplayName` is not stored with the session: `SessionsRepository.GetAsync` / `GetAllAsync` read only the host's display name from the profile (never the profile with its refresh token) and hand it over with `WithHostDisplayName`.
- Only an admin creates, deletes or releases sessions (`/admin/sessions...`). The host or an admin renames a session and changes its password. `GET /sessions` (id, code, name and only whether there is a host, `SessionListItemDto`: never who it is) and `GET /sessions/{id}` (with the host's name) are public. The admin panel lists the sessions with their hosts' names through `GET /admin/sessions` (admin only).

## Session password and QR join

### Password

- Every session has a mandatory password (`Session.PasswordHash`, PBKDF2 through `IPasswordHasher`, 4-100 characters), set by `POST /admin/sessions` and changed by the host or an admin through `PUT /sessions/{id}/password`.
- Everyone who joins, admins included, unlocks the session with `POST /sessions/{id}/join` (rate limited, 10 per minute per IP). It answers 401 for a wrong password and for an unknown session alike, and verifies against a dummy hash so timing doesn't tell them apart.
- `SessionAccessService.JoinAsync` returns an access stamp (hash of the password hash). `ISignedCookies.GrantSessionAccess` stores it in the signed `cookie.partyKing.sessions` cookie together with the session id (at most 20 sessions).
- `[RequireSessionAccess]` (`SessionAccessFilter`) checks the cookie against the route's `sessionId` and answers 401 on every session endpoint (`GET /sessions/{id}`, `/me`, `now-playing`, `songs` list/add/remove, `songs/next`, `tracks`, `host-login-urls`, `player/state`) and on the admin player endpoints (`admin/sessions/{id}/player/commands` and `songs/next`).
- Changing the password changes the stamp, so old unlocks stop working. `ChangePasswordAsync` returns the new stamp and the controller grants it to the caller again, so the host or admin who changed it stays unlocked.
- Delete and release host are admin-only and need no session password. Rename (`PATCH /sessions/{id}`) and change password are host-or-admin (`RenameAsync` / `ChangePasswordAsync` take the `SessionCaller` and answer Forbidden otherwise) and, being `[RequireSessionAccess]`, need the session unlocked, so an admin who doesn't know the password can't reset it (accepted).
- UI: `SessionPasswordForm` is shown on `/s/:sessionId` when `getSession` answers 401; the axios instance sends credentials on every call.

### Join with QR code

- The host and admins (`useViewerRole`) get a "Show QR code" button (`JoinQrCode`, MUI `Dialog`) on the session page.
- `POST /sessions/{id}/join-token` (`[RequireSessionAccess]`, 403 unless host or admin) returns a stateless token (`JoinTokenProtector`, Data Protection; it holds session id, access stamp and a 5-minute expiry; no DB table). The dialog shows a QR of `/s/{id}?join=<token>`, replaced 30 s before it expires.
- `MusicQueue` redeems `?join=` through `POST /sessions/{id}/join-with-token` (same `session-join` rate limit, plain 401 on any failure), which grants the same session cookie as `/join`, then strips the param.
- A token is reusable until it expires and can't be revoked individually, but changing the password changes the stamp and kills all of them.

## Session host (Spotify login)

- One host per session, and one Spotify account can host one session (unique index on `Session.HostProfileId`). Spotify profile ids are strings; `SpotifyProfile.DisplayName` is shown as "Current Host".
- **Flow**:
  1. `POST /sessions/{id}/host-login-urls` stores a short-lived `OAuthState` (the OAuth `state` parameter, single use) and returns the Spotify URL.
  2. Spotify redirects to the fixed `/callback`, which posts `{code, state}` to `POST /authenticate`.
  3. `SessionsRepository.TryClaimAsync` atomically claims the session: first account wins, others get 409, the current host logging in again is fine.
- An admin can release the host (`DELETE /admin/sessions/{id}/host`).
- **`OAuthState`**: `OAuthState.Issue(sessionId, now)` makes the 48-character hex id from 24 random bytes (kept in the Domain because how unguessable it is is part of the rule); `Lifetime` is 10 minutes; `IsExpired(now)` is true only after `ExpiresAt`; `SpotifyService` fills `now` from `IDateTimeProvider`. `IOAuthStatesRepository` is a Domain port: `AddAsync(state, now)` also forgets the states that expired before `now`; `TakeAsync(id)` returns and deletes the state, the delete deciding who took it, and returns an expired state too, so `AuthenticateByCodeAsync` asks `IsExpired` and an expired or replayed state is used up either way. Tests: `OAuthStateTests` (Domain), `OAuthStatesRepositoryTests`, `SpotifyServiceTests`.
- **`HostTokenProvider`** (singleton) caches and refreshes one access token per host profile, resolved through the session, and returns `Error.Unauthorized` when the refresh token is dead (the host logs in again; "I'm the host" in `SessionHeader`).
- **Playback state**: the host's browser reads `GET https://api.spotify.com/v1/me/player` (scope `user-read-playback-state`; hosts who logged in before it was added get a 403 and just see the normal button until they log in again). While the host has not started the player and the playing song is a Spotify track that this account has on another device (playing or paused), `HostPlayer` shows "Play here" instead of "Start the music" (`takeover.ts`), and `SpotifyPlayer` continues the song with `position_ms` in its `/me/player/play` call, so it starts at the other device's position as a single-track context.
- **Local dev**: the OAuth redirect URI uses `127.0.0.1`, so log in via `http://127.0.0.1:2137/...`, not `localhost` (the Vite dev server redirects `localhost` to `127.0.0.1`; the SameSite=Lax login cookies don't reach the API from a page opened via `localhost`). YouTube refuses to play embeds (player error 150) when the page's origin is an IP address such as `127.0.0.1`, so YouTube songs can't play in local dev; it goes away on a real domain.

## Cookies

- `cookie.partyKing.user` (the host's profile id) and `cookie.partyKing.account` (the account id, its security stamp and its role, of a user or an admin) are signed with ASP.NET Data Protection in `SignedCookies`, so clients can't forge identities. Mind the names: `user` is the Spotify **host**, `account` is a login. `cookie.partyKing.auth` holds the host's Spotify access token; `cookie.partyKing.sessions` the unlocks; `cookie.partyKing.demo` (signed, demo servers only) the demo sessions this browser started with their synthetic host profile ids, so a demo host can be restored after a newer start replaced `cookie.partyKing.user` (see `demo.md`); `cookie.partyKing.ringo` / `.resistance` the game players (see `../games/`).
- Cookies are HTTP, so `ISignedCookies`, `SignedCookies` and the cookie names (`CookieNames`) live in `PartyKing.API/Cookies`, and the scheme names (`SchemeNames`) and the two scheme handlers (`Authentication/Schemes`) in `PartyKing.API/Authentication`. Controllers, the hub and the authentication handlers call it (`GrantHost`, `GrantSpotifyAccessToken`, `GrantAccount` / `RevokeAccount`, `GrantSessionAccess`, `GrantDemoSession` / `ReadDemoSessions`, `GrantGamePlayer`, `TryRead...`), never the services.
- The `CustomCookie` scheme adds `NameIdentifier` (profile id) and `Authentication` (access token) claims; the `AccountCookie` scheme backs `[AuthorizeAccount]` (any signed-in account) and `[AuthorizeAdmin]` (the `Admin` role on top).

## Accounts (users and admins)

One `Accounts` table holds both. A **user** registers on the landing page (it identifies no host, guest or player; the only things attached to it are the playlists it saved from a session's queue, see `music.md`, which are deleted with the account); an **admin** is seeded from configuration. The role decides what the account may do.

### Domain and rules

- `Account` (`Domain/Entities/Auth`): `Id`, `Username`, `PasswordHash`, `Role` (`AccountRole.User` / `Admin`, stored as a string), `SecurityStamp` (a GUID), `CreatedAt`. It has `internal set` properties and is made with `Account.Create(id, username, passwordHash, role, securityStamp, now)`, which returns an `AccountCreation` (`AccountFailure.UsernameEmpty` / `UsernameTooLong`). Id, stamp and time are arguments; the hash is made by `AccountService` through `IPasswordHasher`.
- `AccountUsernames`: trimmed, not blank, at most 50 characters. The username is unique **whatever the case** (the `Username` column is `NOCASE`; SQLite folds only ASCII letters). `AccountPassword`: 8-100 characters, not trimmed (`CanMatch` is the pre-hash length guard of the logins, like `SessionPassword.CanMatch`).
- `ChangePassword(hash, newStamp)` and `SignOutEverywhere(newStamp)` replace the stamp. The stamp is part of the account cookie, so **every cookie issued before dies**: that is how a password change, an admin's reset and "log out everywhere" end the other browsers' logins.

### Services (`Application/Accounts`)

- `AccountService`: `RegisterAsync` (409 for a taken username, which also logs the new user in), `LoginAsync` (401 "Invalid username or password" for anything wrong; verified against `AccountDummyHash` for an unknown name, so timing does not tell them apart; a password longer than 100 characters costs no hash), `ValidateAsync(id, stamp)` (what the cookie handler asks on **every** request: the account must exist, the stamp match, and the role is the one it has now), `ChangePasswordAsync` (needs the current password; a wrong one is a 400, not a 401, because the UI reads 401 as "logged out"), `SignOutEverywhereAsync`, `DeleteAsync` (users only, with the password; 403 for an admin).
- `AccountAdminService`: `ListUsersAsync` (users only, ordered by username, page clamped to 1+ and size to 1-100, default 20; returns `PagedDto<AccountDto>`) and `ResetPasswordAsync` (sets a password and a new stamp; 403 for an admin account, 404 for an unknown id). Whether the caller is an admin is the API's to check.
- Costs: `PasswordHasher.UserIterations` = 200 000 for users (about 19 ms), `AdminIterations` = 600 000 for the seeded admin (about 56 ms), `SessionIterations` = 100 000. The count is stored in the hash, so hashes of any cost verify. `AccountDummyHash` has the user cost, so an admin's login takes longer than an unknown name's (which tells which usernames are admins; accepted, and registration already reveals that a name is taken).
- `AdminSeeder` creates the first admin at startup when **no admin** exists, from `AdminSettings:Username` plus either `Password` (plain text, hashed on seeding) or `PasswordHash`; put them in the untracked `appsettings.Local.json`. If a user has already taken that username, it logs an error and creates nothing. Generate a hash with `dotnet run --project PartyKing.API -- hash-password` (from `src/api`; it prompts for the password and prints the value, without starting the app).
- The repository (`IAccountsRepository`) adds with `TryAddAsync` (false on the unique index), pages with `GetUsersPageAsync(search, page, pageSize)` (a `LIKE` substring search that escapes `%`, `_` and `\`), and updates through the domain actions. Admin passwords can only change by editing the row or reseeding.

### API and cookie

- `POST /account/register`, `POST /account/login` (both 204 and set the cookie), `POST /account/logout`, `POST /account/logout-everywhere`, `GET /account/me` (`AccountDto`; 401 when nobody is signed in), `PUT /account/password`, `DELETE /account` (body `{password}`). Login, password change and delete share the `account-login` rate limit (20 per minute per IP: guests at a party share one Wi-Fi), registration `account-register` (10 per minute per IP). There is no per-account lockout.
- Admin: `GET /admin/users?search=&page=&pageSize=` and `PUT /admin/users/{id}/password` (`{newPassword}`), both `[AuthorizeAdmin]`. The old `/admin/login`, `/admin/logout` and `/admin/me` are gone: admins log in through `/account/login`.
- The cookie carries `accountId:stamp:role` in a **time-limited** Data Protection payload (purpose `PartyKing.AccountCookie`): 12 hours for an admin, 30 days for a user; the lifetime is signed in, not only a cookie attribute. `AccountCookieAuthenticationHandler` calls `ValidateAsync` and adds the `NameIdentifier` and `Role` claims; a changed password, a stamp that no longer matches or a deleted account fails the authentication.
- **No database read for a user on the polled endpoints.** `ReadSessionCallerAsync` runs on the session endpoints that clients poll (every game fetch among them) and every registered guest has an account cookie. The role is signed into the cookie (`id:stamp:role`), so a `User` cookie is ruled out as an admin without a lookup, and only a cookie that says `Admin` is checked against the database (admins are few). The cost: a user promoted to admin by editing the row is an admin in sessions only after the next login. `[AuthorizeAccount]` and `[AuthorizeAdmin]` endpoints still validate the account and stamp on every request (they are not polled).
- **A signed-in user is not an admin.** `[AuthorizeAdmin]` is the `Admin` policy (`AccountCookie` scheme plus the `Admin` role), and `HttpContext.ReadSessionCallerAsync` sets `SessionCaller.IsAdmin` only for an account whose role is `Admin`. `AccountAuthenticationTests` pins both against the real wiring.
- Changing the password re-grants the cookie with the new stamp, so the browser that changed it stays signed in; "log out everywhere" revokes this browser's too.

### UI

- Every page has the account button (`AccountMenu`, upper-right corner). For a visitor it reads "Log in" and opens `AccountDialog` (log in / sign up tabs). Signed in, it reads the username with a person icon and drops a menu: "Account settings" opens the dialog (change password, log out everywhere and, users only, delete account) and "Log out" logs this browser out.
- `useAccount` (`service/endpoints/account`) keeps who is signed in. A localStorage hint (`partyking.accountHint`) makes the landing page ask `GET /account/me` only for browsers that signed in before. The admin pages pass `alwaysCheck`, since they exist for signed-in accounts, and an account found sets the hint.
- `/admin` shows the login form to a visitor, a "not an admin account" notice to a user, and the panel to an admin, who gets a link to `/admin/users`: a paged, searchable list (page size 20, search debounced 300 ms) with a reset-password dialog per user.

Tests: `AccountTests`, `AccountPasswordTests` (Domain), `AccountServiceTests`, `AccountAdminServiceTests`, `AdminSeederTests`, `AccountsRepositoryTests` (real SQLite: case-insensitive uniqueness, paging, search) and `MeteredRepositoryTests` (Application; fake hasher), `SignedCookiesTests`, `AccountAuthenticationTests`, `DevPersonaMiddlewareTests` (Api), and the E2E specs `accounts.spec.ts` and `admin-users.spec.ts`.

## Dev personas

In Development and the e2e environment only (`IsDevelopmentOrE2e`), `DevPersonaMiddleware` (`PartyKing.API/Development`) lets one browser play several guests. The UI (`service/devPersona.ts`) keeps a tab's `?persona=<name>` in `sessionStorage` and sends it as `X-Dev-Persona`. Such a request is a plain guest (host and account cookies hidden) with its own Ringo/Resistance player cookies (stored as `<cookie>.<persona>`), while the session unlock cookie stays shared. `OpenGamesMenu` and `GameInvite` carry the persona into the tabs they open. The hub reads it from `&persona=` on `/hubs/session`.

**All players in one tab** (`ResistanceDevPanels`, route `/dev/s/:sessionId/resistance`, registered in dev builds only, opened by the dev-only "Dev: all players" link in the Resistance navigation for the host and admins): picks 5 to 10 players, joins `dev-1`..`dev-N` through `gamesApi` (the optional `persona` argument sends `X-Dev-Persona` for that one call, so the page itself keeps the admin identity of the tab it was opened from), starts the game on request and shows one same-origin `<iframe>` per player (a grid of up to 2 rows, each panel scrolls on its own). A frame is `isDevEmbedded` (`window.self !== window.top`): it takes its persona from `?persona=` or else from its `window.name` (set by the page, survives a reload and an in-frame navigation that drops the query) and never reads or writes the tab's `sessionStorage`, which a same-origin frame shares with its parent. A frame also drops the language switcher and, in `Resistance`, the navigation, phase banner, board and queue, so it shows only the player's own view. The join needs the session unlocked in this browser (the unlock cookie is shared by personas) and, past 10 joins a minute, `RateLimiting__Disabled=true` on the API.

## Session aggregate, events and handlers

### Aggregate

- `Session` is an `AggregateRoot` with `internal set` properties. Actions: `Rename`, `ChangePassword`, `ClaimHost`, `Release`, `SetFallbackPlaylist`, `SetFallbackSavedPlaylist`, `ClearFallbackPlaylist`; queries `NextFallbackTrack` and `NextFallbackSavedSong`. Inside `Session` the `FallbackPlaylistKind` enum members are written `Enums.FallbackPlaylistKind.X`, because the property of the same name hides the type. Id and time are arguments (the services fill them from `Guid.NewGuid()` and `IDateTimeProvider`).
- **Name rule**: `SessionNames` in Domain (trim, then at most 60 characters). `Session.Create(id, code, name, passwordHash, now)` falls back to "New session" for a blank name and refuses a long one with a `SessionCreation`. `Rename` returns a `SessionNameFailure` instead of throwing, and `ISessionsRepository.RenameAsync` a `SessionRenameResult`. `SessionNamePolicy` in Application only maps the failure to errors.
- **Password rule**: the `SessionPassword` value object (4-100 characters; `Create` returns a `SessionPasswordCreation`; `CanMatch` is the pre-hash length guard of `JoinAsync`). `Session.AccessStamp` / `AccessStampOf(hash)` / `IsUnlockedBy(stamp)` hold the stamp rule.

### Events

`SessionRenamed`, `SessionPasswordChanged` (no hash), `HostClaimed`, `HostReleased(PreviousHostProfileId)`, `SessionDeleted(HostProfileId, FallbackPlaylistId)` (a Spotify id only, a saved playlist has nothing to evict), `FallbackPlaylistChanged(Kind, PlaylistId, PreviousSpotifyPlaylistId)` (one event for both kinds; the previous id is there so the handler can evict the cached tracks of a Spotify playlist that was replaced, null when it was a saved one) and `FallbackPlaylistCleared(PreviousPlaylistId)` (raised by `Release` too, and never when there was no playlist; the id is null when a saved playlist was removed). In `Domain/Events/Sessions`.

### Repository

- `SessionsRepository` takes `IDomainEventDispatcher`. Rename, password, fallback playlist and release load the session tracked, run the action, save and dispatch `DrainEvents()` with `CancellationToken.None`. There is no per-session lock; a session deleted under the save answers false.
- `Session.HostProfileId` is a concurrency token: a save based on a stale host (a claim or release landed between the load and the save) matches no row, and `UpdateAsync` runs the action again on a fresh load, at most 3 attempts, then the `DbUpdateConcurrencyException` propagates (a 500). A release of a session that had no host still raises `HostReleased(null)`, which can drop the player state of a host who claimed right after (accepted).
- Two writes stay plain SQL. `TryClaimAsync` loads the session untracked and lets `Session.ClaimHost(profileId)` decide (`HostClaimOutcome`: `Claimed`, `AlreadyHost` for the host logging in again, `HostedByAnother`; it raises `HostClaimed` only for a claim), then writes with a conditional `ExecuteUpdate` on `HostProfileId IS NULL`, which is what makes the first claim win (the unique index on the host still answers `ProfileHostsAnotherSession`); it dispatches the aggregate's events once that update matched a row. `DeleteAsync` raises `SessionDeleted` itself (reads host and playlist first, like `LobbyGamesRepository.DeleteAsync`).
- `SetFallbackSavedPlaylistAsync` goes through `UpdateAsync` like the other fallback actions, so its event is dispatched. `SetFallbackCursorAsync(id, playlistId, expectedCursor, cursor)` and `SetFallbackSavedCursorAsync(id, savedPlaylistId, expectedCursor, cursor)` are bookkeeping and raise nothing. Each is a compare-and-set on the playlist id, the *kind* and the cursor the pick was based on, so a playlist changed or rewound while the filler was reading keeps its new cursor, and an id can never match across kinds.

### Handlers (`Application/Sessions/Handlers`)

- `HostCleanupHandler` (`HostReleased`, `SessionDeleted` → `IHostSessionCleanup`).
- `SlideshowCleanupHandler` (`SessionDeleted`).
- `SessionDeletedPushHandler` (`SessionDeleted`, registered first). The cascade delete took the queue and the games, so clients still connected hear the existing pushes: `RingoChanged` / `ResistanceChanged` (each client fetches again and gets its 404), `ResistanceLobbyChanged`, and an empty `QueueAdvanced` that clears the song on display. The queue list follows with its next poll; a dedicated push is left for later.
- `FallbackPlaylistHandler` (track cache, which it evicts only for Spotify playlists, `FallbackStatusStore`, and `EnsureQueuedAsync` after a change; also on delete).
- `SessionLogHandler`.
- Because handler failures are only logged, a failed cleanup no longer fails the delete or release request. `SessionAdminService`, `SessionService` and `FallbackPlaylistService` do not clean up themselves.
- Tests: `SessionTests`, `SessionsRepositoryEventsTests`, `SessionHandlersTests`; `FakeSessionsRepository` runs the real actions and dispatches to `Events` when set (`TestEvents.SessionDispatcher`).
