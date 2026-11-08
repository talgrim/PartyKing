# Domain events and realtime pushes

Aggregates raise domain events; handlers in Application react to them, mostly by sending payload-free SignalR pushes. Services do not push themselves. Feature-specific events are described with their feature (queue and fallback in `../features/music.md`, session events in `../features/sessions-and-auth.md`, slideshow settings in `../features/slideshow.md`).

## Domain events

- Game aggregates derive from `AggregateRoot` (`Domain/Entities/Base`: protected `Raise`, public `DrainEvents()`; the pending list is a private field, so EF never maps it) and raise `IDomainEvent` records, grouped like the entities in `Domain/Events/Games/{Base,Ringo,Resistance}` (the plot-cards add-on in `ResistanceEvents.PlotCards.cs`, the Inquisitor module in `ResistanceEvents.Inquisitor.cs`).
- Lobby: `LobbyOpened`, `LobbyPlayerJoined`, `LobbyPlayerKicked`, `LobbyOrderChanged`, `LobbyGameEnded`, each with the game's `Kind` (abstract on `LobbyGame`).
- Ringo: `RingoStarted`, `RingoCardFlipped`, `RingoGameOver`, `RingoTurnPassed`.
- Resistance: `ResistanceCreated`, `ResistanceStarted`, `ResistanceRestarted`, `ResistanceTeamSubmitted`, `ResistanceVoteCast`, `ResistanceTeamApproved`, `ResistanceTeamRejected`, `ResistanceMissionCardPlayed`, `ResistanceMissionCompleted`, `ResistanceLeaderChanged`, `ResistanceGameOver`, `ResistancePlotCardPlayed`, `ResistanceInquisitorTokenPassed` (who examined whom, never the loyalty), `ResistanceAssassinated` (which player the Assassin named, published when the game is over), plus the seat events (see `../games/resistance-implementation.md`) and the catch-all `ResistanceChanged`, which every successful action raises through `ResistanceGame.Succeeded()`.
- No event carries a secret: `ResistanceVoteCast` names the voter, not the vote; `ResistanceMissionCardPlayed` not the card.
- `ResistanceFlow` and `ResistanceDealer` (static) raise through `game.Publish` and passes the lead with `game.ChangeLeader`.

## Dispatch

- `IDomainEventDispatcher` / `IDomainEventHandler<T>` live in `Application/Events`. `DomainEventDispatcher` (singleton, in memory, no outbox) gives each event its own DI scope, runs its handlers in order and logs a handler that throws instead of failing the request. So a failed side effect (a cleanup, a push) never fails the request that caused it.
- `LobbyGamesRepository.WriteAsync` hands each write a list; a saved write adds `game.DrainEvents()` to it (open, join, kick, reorder, a changed `UpdateAsync`, `TryCreateAsync`). `DeleteAsync` adds `LobbyGameEnded` itself, from the repository's abstract `Kind`, because it loads no game.
- Events are dispatched after the lock is released, with `CancellationToken.None`, so a handler may write to the same session and an aborted request does not drop the pushes. Handlers run before the repository call returns, so keep them quick.
- Handlers (`Application/Games/Handlers`: `LobbyPushHandler`, `RingoPushHandler`, `ResistancePushHandler`, `ResistanceLobbyPushHandler`) send the pushes. Register one with `AddDomainEventHandler<TEvent, THandler>()` in the `Register*` method of its area.
- In tests, `TestEvents.Dispatcher(...)` builds a real dispatcher with the push handlers over the fake notifiers; the fake game repositories dispatch like the real ones; `RecordingDispatcher` records for repository tests; `FakeQueueRepository` does not dispatch.

## Notifier ports

- Application declares one push port per domain in `Application/Realtime`: `IQueueNotifier`, `IPlayerNotifier` (incl. the fallback error), `ILyricsNotifier`, `ISlideshowNotifier`, `IRingoNotifier`, `IResistanceNotifier`. Each has a no-op default (`NullNotifiers.cs`, `TryAddSingleton`) that the API replaces in `RegisterHubs` with the SignalR classes in `API/Hubs/Notifiers`.
- All SignalR notifiers derive from `SessionGroupNotifier`, which sends to the group of its topic (`SessionHub.GroupName(sessionId, topic)`). Event names are in `SessionMusicHubEvents`; `SignalRNotifierTests` covers them.
- Services inject only the ports they use; tests use one fake per port in `Fakes.cs`.

## Topics

A connection joins the groups named by `?topics=` (comma-separated; default all; an unknown name aborts; `SessionHubTopics.TryParse`). Topics: `music` (queue, player, fallback status, lyrics offsets), `slideshow`, `ringo`, `resistance`, `resistanceLobby` (the lobby-only push), `resistancePresence` (TV layout only).

`useSessionMusicHub(sessionId, enabled, hostPlayer?, topics?)` is called with:

| Page | Topics |
|---|---|
| Ringo board | `['ringo']` |
| Resistance board | `RESISTANCE_HUB_TOPICS` (`resistance`, `resistanceLobby`), plus `music` (`withMusicTopic`) for a player, the host and admins, who have the queue panel there (observers do not) |
| Resistance TV layout | also `RESISTANCE_TV_HUB_TOPICS` (adds `resistancePresence`) |
| Join pages (`GameJoin` `topics` prop) | `['ringo']`; `['resistanceLobby']` for Resistance, which before joining shows only the lobby |
| `MusicQueue` | `MUSIC_QUEUE_HUB_TOPICS`: music, slideshow, ringo and `resistanceLobby` — it needs the Ringo pushes and the Resistance lobby push for the invites and the games menu, but a Resistance round must not wake it |

## Resistance lobby push

- `ResistanceChanged` (every action of a round, topic `resistance`) makes a client fetch the game only. `ResistanceLobbyChanged` (topic `resistanceLobby`, `IResistanceNotifier.LobbyChangedAsync`) makes it fetch the lobby only, because the lobby DTO changes only when the game is created, opened, started, restarted, ended, or a player joins or is kicked.
- `ResistanceLobbyPushHandler` sends it for `ResistanceCreated`, `ResistanceStarted`, `ResistanceRestarted` and, for `Kind == Resistance`, `LobbyOpened`, `LobbyPlayerJoined`, `LobbyPlayerKicked` and `LobbyGameEnded` (not `LobbyOrderChanged`; Ringo's lobby events are ignored). `SessionDeletedPushHandler` sends it too. Those events still push `ResistanceChanged` as before.
- `useSessionMusicHub` fetches the lobby on `ResistanceLobbyChanged` and `ResistanceOpened` after one short debounce (50 ms plus 0-250 ms), so an opening is one lobby GET. After a reconnect it refetches only the keys of its topics (`resistance` the game, `resistanceLobby` the lobby). The Resistance game and lobby hooks do not revalidate on focus; the hub fetches again after a reconnect.
