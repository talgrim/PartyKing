# Sessions, host and authentication

## Contents

- Sessions
- Session password and QR join
- Session host (Spotify login)
- Cookies
- Admin accounts
- Dev personas
- Session aggregate, events and handlers

## Sessions

- A `Session` has a GUID id, a unique short `Code` (from `SessionCodeGenerator`), a name defaulting to "New session" and a nullable `HostProfileId`. It owns one `QueueItem` queue (`QueueItem.SessionId`, cascade delete) and its own slideshow.
- `HostDisplayName` is not stored with the session: `SessionsRepository.GetAsync` / `GetAllAsync` read only the host's display name from the profile (never the profile with its refresh token) and hand it over with `WithHostDisplayName`.
- Only an admin creates, deletes or releases sessions (`/admin/sessions...`). The host or an admin renames a session and changes its password. `GET /sessions` (id, code, name, host) and `GET /sessions/{id}` are public.

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

- `cookie.partyKing.user` (the host's profile id) and `cookie.partyKing.admin` (the admin account id) are signed with ASP.NET Data Protection in `SignedCookies`, so clients can't forge identities. `cookie.partyKing.auth` holds the host's Spotify access token; `cookie.partyKing.sessions` the unlocks; `cookie.partyKing.ringo` / `.resistance` the game players (see `../games/`).
- Cookies are HTTP, so `ISignedCookies`, `SignedCookies` and the cookie names (`CookieNames`) live in `PartyKing.API/Cookies`, and the scheme names (`SchemeNames`) and the two scheme handlers (`Authentication/Schemes`) in `PartyKing.API/Authentication`. Controllers, the hub and the authentication handlers call it (`GrantHost`, `GrantSpotifyAccessToken`, `GrantAdmin` / `RevokeAdmin`, `GrantSessionAccess`, `GrantGamePlayer`, `TryRead...`), never the services.
- The `CustomCookie` scheme adds `NameIdentifier` (profile id) and `Authentication` (access token) claims; the `AdminCookie` scheme backs `[AuthorizeAdmin]`.

## Admin accounts

- Stored in `AdminAccounts` with PBKDF2 password hashes. When none exists, the first one is created at startup from `AdminSettings:Username` plus either `Password` (plain text, hashed on seeding) or `PasswordHash`; put them in the untracked `appsettings.Local.json`.
- Generate a hash with `dotnet run --project PartyKing.API -- hash-password` (from `src/api`; it prompts for the password and prints the value, without starting the app).
- `POST /admin/login` is rate limited (10 per minute per IP).
- There is no password-change feature (no endpoint, no repository update): the password changes only by editing the row or emptying `AdminAccounts` and reseeding, so the aggregate has no `ChangePassword` either.
- **Aggregate**: `AdminAccount` (`Domain/Entities/Auth`) has `internal set` properties and is made with `AdminAccount.Create(id, username, passwordHash)`, which returns an `AdminAccountCreation` (`AdminAccountFailure.UsernameEmpty` for a blank name; the id is an argument; `AdminService` makes the hash through `IPasswordHasher`). The username rule is `AdminUsernames` (trim, must not be blank), used by seeding and by `LoginAsync`. Tests: `AdminAccountTests` (Domain), `AdminServiceTests` (fake hasher; the real one takes 600 000 iterations a hash).

## Dev personas

In Development only, `DevPersonaMiddleware` (`PartyKing.API/Development`) lets one browser play several guests. The UI (`service/devPersona.ts`) keeps a tab's `?persona=<name>` in `sessionStorage` and sends it as `X-Dev-Persona`. Such a request is a plain guest (host/admin cookies hidden) with its own Ringo/Resistance player cookies (stored as `<cookie>.<persona>`), while the session unlock cookie stays shared. `OpenGamesMenu` and `GameInvite` carry the persona into the tabs they open. The hub reads it from `&persona=` on `/hubs/session`.

## Session aggregate, events and handlers

### Aggregate

- `Session` is an `AggregateRoot` with `internal set` properties. Actions: `Rename`, `ChangePassword`, `ClaimHost`, `Release`, `SetFallbackPlaylist`, `ClearFallbackPlaylist`; query `NextFallbackTrack`. Id and time are arguments (the services fill them from `Guid.NewGuid()` and `IDateTimeProvider`).
- **Name rule**: `SessionNames` in Domain (trim, then at most 60 characters). `Session.Create(id, code, name, passwordHash, now)` falls back to "New session" for a blank name and refuses a long one with a `SessionCreation`. `Rename` returns a `SessionNameFailure` instead of throwing, and `ISessionsRepository.RenameAsync` a `SessionRenameResult`. `SessionNamePolicy` in Application only maps the failure to errors.
- **Password rule**: the `SessionPassword` value object (4-100 characters; `Create` returns a `SessionPasswordCreation`; `CanMatch` is the pre-hash length guard of `JoinAsync`). `Session.AccessStamp` / `AccessStampOf(hash)` / `IsUnlockedBy(stamp)` hold the stamp rule.

### Events

`SessionRenamed`, `SessionPasswordChanged` (no hash), `HostClaimed`, `HostReleased(PreviousHostProfileId)`, `SessionDeleted(HostProfileId, FallbackPlaylistId)`, `FallbackPlaylistChanged(PreviousPlaylistId, PlaylistId)` and `FallbackPlaylistCleared` (raised by `Release` too, and never when there was no playlist). In `Domain/Events/Sessions`.

### Repository

- `SessionsRepository` takes `IDomainEventDispatcher`. Rename, password, fallback playlist and release load the session tracked, run the action, save and dispatch `DrainEvents()` with `CancellationToken.None`. There is no per-session lock; a session deleted under the save answers false.
- `Session.HostProfileId` is a concurrency token: a save based on a stale host (a claim or release landed between the load and the save) matches no row, and `UpdateAsync` runs the action again on a fresh load, at most 3 attempts, then the `DbUpdateConcurrencyException` propagates (a 500). A release of a session that had no host still raises `HostReleased(null)`, which can drop the player state of a host who claimed right after (accepted).
- Two writes stay plain SQL. `TryClaimAsync` loads the session untracked and lets `Session.ClaimHost(profileId)` decide (`HostClaimOutcome`: `Claimed`, `AlreadyHost` for the host logging in again, `HostedByAnother`; it raises `HostClaimed` only for a claim), then writes with a conditional `ExecuteUpdate` on `HostProfileId IS NULL`, which is what makes the first claim win (the unique index on the host still answers `ProfileHostsAnotherSession`); it dispatches the aggregate's events once that update matched a row. `DeleteAsync` raises `SessionDeleted` itself (reads host and playlist first, like `LobbyGamesRepository.DeleteAsync`).
- `SetFallbackCursorAsync(id, playlistId, expectedCursor, cursor)` is bookkeeping and raises nothing. It is a compare-and-set on the playlist id and the cursor the pick was based on, so a playlist changed or rewound while the filler was reading Spotify keeps its new cursor.

### Handlers (`Application/Sessions/Handlers`)

- `HostCleanupHandler` (`HostReleased`, `SessionDeleted` → `IHostSessionCleanup`).
- `SlideshowCleanupHandler` (`SessionDeleted`).
- `SessionDeletedPushHandler` (`SessionDeleted`, registered first). The cascade delete took the queue and the games, so clients still connected hear the existing pushes: `RingoChanged` / `ResistanceChanged` (each client fetches again and gets its 404), `ResistanceLobbyChanged`, and an empty `QueueAdvanced` that clears the song on display. The queue list follows with its next poll; a dedicated push is left for later.
- `FallbackPlaylistHandler` (track cache, `FallbackStatusStore`, and `EnsureQueuedAsync` after a change; also on delete).
- `SessionLogHandler`.
- Because handler failures are only logged, a failed cleanup no longer fails the delete or release request. `SessionAdminService`, `SessionService` and `FallbackPlaylistService` do not clean up themselves.
- Tests: `SessionTests`, `SessionsRepositoryEventsTests`, `SessionHandlersTests`; `FakeSessionsRepository` runs the real actions and dispatches to `Events` when set (`TestEvents.SessionDispatcher`).
