# Games: hub, lobby, Ringo, Never have I ever, LLM

Resistance has its own file: `resistance-implementation.md` (rules in `resistance-rules.md`).

## Contents

- Games hub and controllers
- Lobby layer (shared by Ringo and Resistance)
- Game rules in the Domain (actions, outcomes, write lock)
- Ringo
- Never have I ever
- LLM client

## Games hub and controllers

- A games page per session at `/s/:sessionId/games` (`Games`), reached only from the "Games" link on each session row of the admin panel for now. `GET /sessions/{id}/games` returns a static list from `GameService` (`resistance`, `ringo`, `never-have-i-ever`; no DB table), named and described in the request language.
- All of it is `[RequireSessionAccess]` and host or admin through `AuthorizeManagerAsync` (403 for guests, which the UI answers by redirecting to the session page), except where a game opens an endpoint to players (the lobby, join, and the player actions).
- The controller folder mirrors the paths (`Controllers/Games/`). `GameControllerBase` holds the `/games` route constant and `ReadCallerAsync(sessionId, game?)`; `LobbyGameControllerBase` adds the game's `Kind` and `Caller(sessionId)`. Action names leave out the game (`RingoController.Join`). The controllers are `Ringo/RingoController`, `NeverHaveIEver/NeverHaveIEverController` and, in `Resistance/` on one route, `ResistanceController`, `ResistanceRoundController`, `ResistancePlotCardsController` and `ResistanceSeatsController`. The `controller` metric label is the controller name.
- One folder per game under `Application/Games/Services` (`Resistance/`, `Ringo/`, `NeverHaveIEver/`); the shared parts (`GameLobby`, `LobbyErrors`, `GameCaller`, `GameService`, `IGameLobbyNotifier`) are in `Base/` (namespace `...Services.Base`); `SuggestionParser` stays at the root.
- **Open games menu**: `OpenGamesMenu` in `SessionHeader` (everyone who unlocked the session; next to the QR and settings icons for the host and admins) is a gamepad icon, hidden while no game is open, whose menu lists the open lobbies. A choice opens in a new tab: the board for the host and admins, the join page for guests (which shows the board once joined). It replaces separate board/join links, so a guest who dismissed the invitation can still join. For Resistance the host and admins get two tabs from one click: the TV board (`/games/resistance?tv=1`) and the join/player page (only the TV one once the game started without them; a browser may block the second tab).

## Lobby layer

Ringo and Resistance share one lobby.

**Domain**
- `LobbyGame<TPlayer>` (`SessionId`, `CreatedAt`, `IsOpen`, `Players`) and `LobbyPlayer` (name, `NormalizedName`, `Order`, `ProfileId`, `HostSuffix`) are abstract non-entity bases, so each game keeps its own tables.
- The lobby rules are methods of `LobbyGame`, returning a `LobbyStatus`: `Open()`, `Join(player)` (open, not locked, unique normalized name, player limit, then appends the player and sets session, order and normalized name), `Kick(id)` (removes the player and closes the gap in the order) and `Reorder(ids)`.
- A game customizes them through the protected `PlayerLimit` and `IsLocked` and the hooks `OnPlayerJoined` / `OnPlayerKicking` (`ResistanceGame`: `MaxPlayers` and `IsStarted`; `RingoGame`: the turn).
- `LobbyPlayer.NormalizedName` is derived from the display name unless a stored value was set (its backing field is deliberately not named after the property, so EF goes through the getter). `LobbyNames` holds `MaxLength`, `HostName`, `Validate` (a `LobbyNameFailure`) and `Normalize`.

**Infrastructure**
- `LobbyGamesRepository<TGame,TPlayer>` (Get, TryCreate, Open, Join, Kick, Reorder, Delete and the generic `UpdateAsync`) only loads, takes the write lock, runs the `LobbyGame` method and saves, handling unique violations and a vanished game. It holds no lobby rules. Wrapped by `MeteredLobbyGamesRepository`.

**Application**
- `GameLobby<TGame,TPlayer>` (authorization, open, join, kick, reorder) with `GameLobbyOptions`, subclassed by `RingoGameLobby` and `ResistanceGameLobby` (each fixes the options). The lobbies are registered in DI; services just inject the lobby.
- `LobbyErrors` is an instance built from the options that maps a `LobbyStatus` or `LobbyNameFailure` to `Error`s, pinned by the golden table in `LobbyErrorsTests`.
- `IRingoNotifier` / `IResistanceNotifier` extend `IGameLobbyNotifier` (`OpenedAsync` / `ChangedAsync`); only the push handlers use them.
- Player cookies go through `ISignedCookies.GrantGamePlayer` / `TryReadGamePlayer` by `GameKind` (`cookie.partyKing.ringo`, `cookie.partyKing.resistance`).
- **Admin join**: an admin may join under a typed name or leave it blank, which makes them "admin" (Ringo and Resistance).

**UI**: `lobby/GameInvite`, `lobby/GameJoin` and `lobby/GamePlayersDialog` are the shared parts of `RingoInvite` / `RingoJoin` / `RingoSettings` and their Resistance twins.

## Game rules in the Domain

- Entities are grouped by game under `Domain/Entities/Games`: `Base/` (`LobbyGame`, `LobbyPlayer`, `LobbyNames`, `LobbyStatus`, `LobbyResult`, `GameKind`, `IGameUpdateResult`), `Resistance/` and `Ringo/`, each with its own namespace (`PartyKing.Domain.Entities.Games.Base|Resistance|Ringo`; the model snapshot names the entities by those namespaces).
- The state machines are methods of the aggregates, not of the repositories. `ResistanceGame` is a `partial` class: `ResistanceGame.cs` holds `RefreshRules`, `SetPlotCards`, `Start`, `Restart`, `SelectTeam`, `Vote`, `PlayMissionCard`, `Continue`; `ResistanceGame.PlotCards.cs` the plot-card actions; `ResistanceGame.Seats.cs` the seat takeover.
- An action returns a `ResistanceOutcome(ResistanceFailure? Failure, ResistanceGame? Game)`; `RingoGame.Flip` / `Continue` return a `RingoOutcome` (same shape plus an explicit `Changed`, because Ringo accepts some actions that change nothing). Both implement `IGameUpdateResult`. A `ResistanceFailure` member exists per distinct error code and message, so two refusals worded differently are two members even when they sound alike.
- Time and randomness are arguments (`startedAt`, `playedAt`, `Random`), filled by the services from `IDateTimeProvider` and `Random.Shared`, so Domain tests need no fixed clock or seed.
- The started/game-over guard of the actions is the private `Inactive()` helper. An action ends with `Succeeded()`, which raises the `ResistanceChanged` catch-all (plus its own event, if it has one).
- `ResistanceRules.Clone(plotCardsEnabled?)` is the deep copy used by `SetPlotCards`, `RefreshRules` and `ResistanceRulesProvider`; every new `ResistanceRules` property must be copied there.
- Plot-card actions check ownership first (403 `NotYourCard`), then the card's type and location (`UnknownCard`), then the phase, so a caller probing card ids learns nothing about other players' hands. `ConfirmPlotCard` accepts the holder or the viewer.

**Running an action.** A service calls `ILobbyGamesRepository.UpdateAsync(sessionId, operation, noGame, game => game.Vote(...), ct)`. It loads the game, runs the synchronous action, saves only when the outcome `Changed`, and does all of it under a per-session write lock.
- The lock (`WriteAsync`, 64 striped `SemaphoreSlim`s per repository instance) is also used by Open, Join, Kick, Reorder, TryCreate and Delete. It is in-process only, like `QueueRepository`'s advance lock, and its wait is recorded per repository and operation. `operation` is the metric label, `nameof` of the service method.
- The lock is what makes "the last vote tallies the round" and "the last mission card resolves the mission" safe when clients act at once; without it each request loaded a game without the others' changes and the round was never counted.
- A game can still vanish under a write, because the cascade delete of a session bypasses the lock. When a save fails with a `DbUpdateException` and the game no longer exists, `UpdateAsync`, Open, Kick and Reorder answer `noGame` / `NoGame` (a 404, never a 500). Join does the same unless the failure is a unique violation (`DbUpdateException.IsUniqueViolation()` in Persistence), the only case reported as `NameTaken`. `TryCreateAsync` treats only a unique violation as "already exists". Anything else is rethrown.

**Adding an action**: a Domain method returning the outcome (new failures get a member and a row in `ResistanceFailureErrorsTests.Golden` / `RingoErrorsTests`), a service method calling `UpdateAsync`, a Domain test with no database. The repository stays untouched.

**Encapsulation**: game entity properties have `internal set` (Domain has `InternalsVisibleTo` for the Application tests, which still build states with initializers). Games and players are made with `RingoGame.Start`, `ResistanceGame.Create` and `ILobbyPlayerFactory<T>.Create` (`GameLobby` requires it instead of `new()`); the constructors are internal. In tests, `TestEvents.Dispatcher(...)` builds a real dispatcher with the push handlers over the fake notifiers, the fake game repositories dispatch like the real ones, and `RecordingDispatcher` records for the repository tests.

## Ringo

### Game and endpoints

- At most one game per session: `RingoGame` (key `SessionId`, cascade delete; `Seed`, `PendingCardId`) and its `RingoFlip` rows (key `SessionId` + `Order`; `CardId` 0-51, `FlippedAt`, `FlippedBy`). `FlippedBy` is a placeholder for now: "admin" when the admin cookie authenticates, else "guest"; with players, the flip history records the player's display name.
- `RingoService` (all actions host or admin through `AuthorizeManagerAsync`) behind `RingoController`:
  - `GET games/ringo`: 404 without a game. Open to managers and joined players (403 for others, which the UI turns into the join page); returns `isOpen`, `players`, `currentPlayerId`, `yourPlayerId`.
  - `POST games/ringo`: starts one with a random seed; 409 when one exists, so an existing game can only be resumed.
  - `POST games/ringo/cards/{cardId}/flip`: 400 `Ringo.InvalidCard` outside 0-51 (checked first of all in `RingoGame.Flip` with `RingoFailure.InvalidCard` and `RingoGame.CardCount`, so a session without a game answers 404 before it). A flipped card changes nothing; otherwise the card is recorded and becomes `PendingCardId`. 409 while another card is pending and after the fourth king (`RingoGame.IsOver`).
  - `POST games/ringo/continue`: clears `PendingCardId`, idempotent.
  - `DELETE games/ringo`: ends it; 404 without one.

### Players and turns

- An admin opens the game with `POST games/ringo/open` ("Open the game" in the nav of the Ringo board, admin only, disabled once open; idempotent, no close; pushes `RingoOpened` and `RingoChanged`).
- Guests and the session host (not admins, who opened it) then get `RingoInvite` (a non-modal MUI `Snackbar` on the session page; dismissal kept per session and seed in `sessionStorage`; "Tak" opens `/s/:id/games/ringo/join` in a new tab). The host and admins get a "Ringo board" link (new tab) in `PageNavigation` while it is open.
- `GET games/ringo/lobby` (anyone unlocked) returns `{seed, isOpen, joined}`.
- `POST games/ringo/join` (`{name}`, `session-join` rate limit) adds a `RingoPlayer` (key `SessionId` + `PlayerId`, `Order`, unique `NormalizedName` per session; names up to 30 characters, and a typed name ending in "(host)" is rejected) and grants the signed `cookie.partyKing.ringo` cookie (session id and player id pairs). A kicked player can join again. The session host joins as "<first word of the Spotify DisplayName> (host)" (`RingoService.HostName`, `RingoPlayer.ProfileId`); the admin under a typed name or as "admin".
- `RingoGame.CurrentPlayerId` is the turn. Players flip only on their turn (409 `Ringo.NotYourTurn`, checked inside `RingoGame.Flip` under the write lock). Continue passes the turn to the next player in `Order` and is allowed for the current player, the host and admins. The host and admins can always flip, and with no players they flip freely.
- `DELETE games/ringo/players/{id}` kicks (host or admin; the turn moves on through `RingoTurns.AfterRemoving`). `PUT games/ringo/players/order` (`{playerIds}`, 409 when the set differs) reorders.

### Push

Start, flip, continue and end are pushed by `RingoPushHandler` / `LobbyPushHandler` (from `RingoStarted`, `RingoCardFlipped`, `RingoTurnPassed` and `LobbyGameEnded`) through `IRingoNotifier.ChangedAsync`, which sends the payload-free `RingoChanged` to the whole session group (guests are in it but get 403 on the GET). `useSessionMusicHub` revalidates the `ringoKey` SWR cache on it, and after a reconnect, so the page does not poll.

### UI

- `RingoEntry` on the Ringo row of `Games` shows "Resume" or "Start new". `Ringo` (`/s/:sessionId/games/ringo`, full page, no `max-w-xl`) puts `RingoBoard` in the middle 80% and opens the hub connection once the game is loaded.
- `createLayout(seed)` (`ringoLayout.ts`, seeded mulberry32) gives every card a position, tilt and unique z-order, so the same seed scatters the cards identically on every device. Cards overlap on purpose and some end up covered.
- Clicking a face-down card flips it (optimistic SWR update, rolled back on error). The pending card stays face up and `RingoCardOverlay` shows it in the middle with its Polish name and effect (`ringoCards.ts`, from the rules in `CLAUDE.local.md`) until "Kontynuuj"; the other cards are disabled meanwhile. Then the card leaves the table, kings and aces going to the piles in the nav bar (`RingoPiles`, "Króle n/4", "Asy n").
- The fourth king ends the game: `RingoGameOver` replaces the overlay with "Koniec gry!", who flipped it and a "Zakończ grę" button that ends the game without confirmation; the board stays locked. "End game" in the nav asks for confirmation in a MUI `Dialog`.
- `RingoHistory` is a right drawer (collapsed by default, `partyking.ringoHistoryOpen` in `localStorage`) listing the flips newest first ("20:02: admin odwrócił/a kartę: As Pik").
- `RingoJoin` (name form, then the board; a kick shows the form again with "Usunięto cię z gry."), `RingoPlayers` seats the players along the left, bottom and right edges (`ringoSeats.ts`), `RingoSettings` ("Players", host and admins) is a dialog with a `@dnd-kit` sortable list for the turn order plus a kick button.

### Ace rule suggestions

- When the pending card is an ace (`getCard(id).rank === 'A'`), `RingoCardOverlay` shows `RingoRuleSuggestions` ("Zaproponuj zasady").
- `POST /sessions/{id}/games/ringo/rule-suggestions` (host or admin through `AuthorizeManagerAsync`, rate limit `game-suggestions`, the same per-IP bucket as the Never have I ever suggestions) takes `{previousRules}` and returns `{suggestions, fromFallback}`. More than 50 previous rules, or one over 200 characters, gives a 400.
- `RingoRuleSuggestionService` asks the LLM for 5 new rules in the request language (parsed by the shared `SuggestionParser`), adds "Nie powtarzaj tych zasad: ..." (English: "Do not repeat these rules: ...") to the prompt, drops returned rules that match a previous one (case-insensitive) and falls back to a built-in list in that language (skipping previous rules; shown again when all were used).
- The server stores no rules. `Ringo` keeps the suggested rules in state and passes them through the overlay, so they survive the overlay closing after each card but not a reload. They are local to the device that asked (no push).

## Never have I ever

`GET /sessions/{id}/games/never-have-i-ever/suggestions` (same auth, rate limit `game-suggestions`, 10 per minute per IP) returns `{suggestions, fromFallback}`. `NeverHaveIEverSuggestionService` asks an LLM for 5 spicy statements in the request language ("Nigdy w życiu nie…" in Polish, "Never have I ever…" in English; JSON array, parsed by `Parse`: blanks, duplicates and statements over 200 characters dropped) and falls back to a built-in list in that language when there is no API key, the call fails or the answer can't be parsed (`fromFallback`; the UI shows a notice). `NeverHaveIEverSuggestions` shows the button and the 429 message.

## LLM client

- `PartyKing.Infrastructure.Llm` registers an `IChatClient` (Microsoft.Extensions.AI) on the OpenAI SDK pointed at Gemini's OpenAI-compatible endpoint (free tier).
- `LlmSettings`: `ApiKey` (secret; locally in the untracked `appsettings.Local.json`, on Fly `LlmSettings__ApiKey`), `Model`, optional `FallbackModel`, `ReasoningEffort` (`low` / `medium` / `high`; Gemini rejects `minimal`; default `low`) and `BaseUrl`.
- The SDK's own retries are off (`maxRetries: 0`) and the network timeout is 30 s. `ProviderErrorChatClient` turns an SDK `ClientResultException` into `LlmProviderException` (`IsTransient` for 429 and 5xx). When `FallbackModel` is set, `FallbackChatClient` retries on it only after a transient error or a timeout (no `ReasoningEffort` there), not after 400/401/404 or the caller's cancellation.
- Without an `ApiKey` the registration returns `UnavailableChatClient` (every call throws, no request leaves the server) instead of building the OpenAI client. The SDK throws on an empty key, and the Ringo and Never have I ever controllers build the suggestion services (and so the chat client) on every request, so each of their requests was a 500. The suggestion services catch the failure and answer with their built-in lists (`LlmRegistrationTests`).
- Microsoft.Extensions.* packages are on 9.0.10 across the projects because Microsoft.Extensions.AI 9.10.2 needs it.
