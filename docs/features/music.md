# Music: queue, player, playlist, lyrics

## Contents

- Queue (endpoints and rules)
- Queue aggregate and events
- Fallback playlist
- Player state and remote control
- Lyrics, position sync, shared offset

## Queue

- `QueueItem`s (YouTube or Spotify) live in the `QueueItems` table, managed by the singleton `QueueRepository`, which has no in-memory queue state except a per-session advance lock. Everything is scoped by session.
- **Add**: guests add songs without logging in via `POST /sessions/{id}/songs` (body `{input}`: a Spotify track URL/URI or a YouTube URL). `SongInputParser` and `PlaylistInputParser` are pure value-object parsers in `Domain/Entities/Queue`, tested in `PartyKing.Domain.Tests`. Search Spotify with `GET /sessions/{id}/tracks?query=`.
- **YouTube**: `SongInputParser` extracts the video id (playlists, channels and searches are rejected). `QueueService` looks the video up through the keyless oEmbed endpoint (`IYoutubeOEmbedClient`): title, author and thumbnail fill the `QueueItem`; a video whose owner disabled embedding is rejected with a 400; when YouTube can't be reached the link is queued with the URL as its title.
- **List**: `GET /sessions/{id}/songs` lists the queued items only. The song being played (status `Playing`) is not among them; it comes from `GET /sessions/{id}/now-playing`.
- **Advance**: `POST /sessions/{id}/songs/next?currentSongId=` is host-only (`[AuthorizeByCookie]` plus a check that the cookie's profile id is the session's `HostProfileId`; 403 otherwise). `currentSongId` is the song the player thinks is playing (none when omitted). `QueueRepository.AdvanceAsync` advances only when that is really the `Playing` item, otherwise it advances nothing; it returns a `QueueAdvanceResult` (`Playing`, `Advanced`, `PreviousSongId`), so the previous song comes from the same locked read as the advance.
- The host's end-of-song call (`QueueService.GetNextAsync`) and an admin's skip (`PlayerService.SkipAsync`) both go through `IQueueAdvancer`, which tops up the queue before advancing. That keeps the host's end-of-song call from skipping a song an admin already skipped.
- **Remove**: `DELETE /sessions/{id}/songs/{songId}` removes a song that is still `Queued`. It has no auth attribute: the controller reads the host cookie and the admin cookie (like `GET /me`) and `QueueService.RemoveAsync` answers 403 unless the caller is an admin or the session's host (404 for an unknown session or song, 409 when the song is already playing or played). `QueueRepository.RemoveAsync` hard-deletes the row under the same per-session lock as `AdvanceAsync` and only matches songs of that session.
- `GET /spotify/token` needs a host login but is per browser, not per session.

## Queue aggregate and events

- `SessionQueue : AggregateRoot` (`Domain/Entities/Queue`) holds the queue rules: `Enqueue(item)`, `EnqueueIfNoneQueued(item)` (null when a song is waiting), `Advance(currentItemId)` (a `QueueAdvanceResult`; a caller whose view is outdated advances nothing) and `Remove(itemId)` (a `QueueRemoval(QueueItemRemoval Outcome, QueueItem? Item)`, the item being what the caller deletes).
- It has no table. `QueueRepository` builds it with `SessionQueue.Of(sessionId, items)` from the rows of one session loaded tracked (the playing and waiting songs, plus the requested one for a removal so a played song answers `NotQueued` and not `NotFound`; played songs are otherwise not loaded), runs one action, saves and dispatches `DrainEvents()`.
- Events (`Domain/Events/Queue`): `SongQueued`, `SongRemoved`, `QueueAdvanced(SessionId, PreviousSongId, NowPlaying)`. They are raised by the action, before the save; `SongQueued` carries the same `QueueItem` instance, whose DB-generated id is set by the time a handler runs. A call that changes nothing (an outdated `currentItemId`, a remove of an unknown or non-waiting song, a refused `AddIfNoneQueuedAsync`) raises nothing.
- `QueueItem` is an entity with `internal set` properties; the constructor takes `isFallback`; `StartPlaying()` / `MarkPlayed()` are internal and guard its status; Mapperly's `ToQueueItem` goes through the constructor.
- The repository collects the events inside each write (`WriteAsync`, under the session's advance lock) and dispatches them after the lock is released, with `CancellationToken.None`. `AddAsync` has no lock, loads nothing (an empty `SessionQueue`) and dispatches right after its save.
- Handlers (`Application/Queue/Handlers`): `QueuePushHandler` (the three `IQueueNotifier` pushes) and `FallbackRefillHandler` (`EnsureQueuedAsync` after `QueueAdvanced` and `SongRemoved`). Push handlers are registered before the refill, so clients hear `QueueAdvanced` and then the refill's `SongAdded`.
- Tests: `SessionQueueTests` (Domain), `QueueRepositoryEventsTests`, `QueueHandlersTests`, `QueueAdvancerTests`.

## Fallback playlist

The host (or an admin) can set a Spotify playlist the host owns that plays whenever the queue is empty.

- `Session` holds `FallbackPlaylistId` / `FallbackPlaylistName` / `FallbackShuffle` / `FallbackCursor`. `QueueItem.IsFallback` marks the songs the server queued from it (the UI shows a "from playlist" badge). Releasing the host clears the fallback fields (the playlist belongs to the host who chose it).
- `PUT /sessions/{id}/fallback-playlist` (`FallbackPlaylistController`, `[RequireSessionAccess]`, host or admin through `AuthorizeManagerAsync`) takes `{playlist, shuffle}` (link or `spotify:playlist:` URI, parsed by `PlaylistInputParser`; empty clears it). `FallbackPlaylistService` checks with the *session host's* token that the playlist's owner is the host (400 otherwise, 409 when the session has no host). To change only the shuffle flag, save the playlist again.
- `GET /sessions/{id}/host-playlists` (host only) lists the playlists the host owns for the picker in `FallbackPlaylistForm`. It needs the `playlist-read-private` scope, so hosts who logged in earlier get 409 "log in again".

**Refill**
- `IFallbackQueueFiller.EnsureQueuedAsync` keeps one `Queued` fallback song in the queue whenever it has none, so guests' songs land behind it and the playlist continues after them. It adds through `IQueueRepository.AddIfNoneQueuedAsync` (same per-session lock as advance/remove, so a guest's concurrent add wins); `AddIfNoneQueuedAsync` raises `SongQueued`, so the filler does not push.
- It runs before every advance (`QueueAdvancer`, which cannot leave this to a handler because it has to precede the advance), after every advance and removal (`FallbackRefillHandler`) and after the playlist is saved (`FallbackPlaylistService`).
- Tracks come from `GetPlaylistTracksAsync` (episodes and local files skipped), cached 5 minutes (`PlaylistTrackCache`). The pick is `Session.NextFallbackTrack(tracks, playingUrl, random)`, a `FallbackPick`: in order, the track at `FallbackCursor` (which wraps around) plus the cursor to store next; shuffled, a random track but not the playing one unless it is the only one, with no cursor. The filler only fetches, queues and stores the cursor.
- A failure (no host, dead Spotify login, empty or unreadable playlist) never throws: `FallbackStatusStore` keeps the message per session in memory and `POST player/state` returns it as `fallbackError`, which `HostPlayer` shows.

## Player state and remote control

- The music plays in the host's browser tab (`HostPlayer`), so the server only knows what that tab reports. `PlayerStateStore` (in-memory singleton behind `IPlayerStateStore`) keeps per session what the host's player last reported (song id, paused, time). The player counts as connected when it reported within 30 s (`PlayerStateStore.ConnectionTimeout`).
- The host's player calls `POST /sessions/{id}/player/state` (`{songId, paused}`, host-only) every ~10 s as a keepalive and at once on every change. `PlayerStateStore.Report` says whether the report changed anything; `PlayerStateChanged` is broadcast only then.
- The response carries the one pending admin command (`Pause` / `Resume`) and `fallbackError`, but both are also pushed over SignalR (`PlayerCommand` from `PlayerService.SendCommandAsync`, `FallbackErrorChanged` from `FallbackStatusStore` when the message changes). The hub is role-agnostic, so they go to the whole session group and only `HostPlayer` acts on them, through the `pushHandlers` ref that `MusicQueue` passes to `useSessionMusicHub`.
- The pending command stays in the store until the next report, so a push missed during a reconnect is still delivered (applying it is idempotent), and the player reports right after a SignalR reconnect.
- `GET /sessions/{id}/now-playing` (public, `PlayerService`) returns the playing song (title, artists, album art or thumbnail), `paused` (only if reported for that very song) and `playerConnected`.
- An admin who is not the host has no host cookie, so `POST /sessions/{id}/player/commands` (`[RequireSessionAccess]`, host or admin through `AuthorizeManagerAsync`, 403 otherwise; `PlayerService.SendCommandAsync` takes the `SessionCaller`) queues a pause/resume command, and `POST /admin/sessions/{id}/songs/next` advances the queue (the host's own `POST songs/next` does that too, without the connected check). Both commands answer 409 when no player is connected; the host's player picks a skipped-to song up by following `now-playing`.
- `GET /sessions/{id}/me` (public, accepts either cookie) returns `{isHost, isAdmin}` so the UI knows whether to show controls.

## Lyrics

- Everyone who unlocked the session gets the drawer and the maximized view with line-synced lyrics. The host and admins can also switch them to the slideshow (guests have no slideshow, so they never request photos, and `SlideshowControls` `lyricsOnly` hides the switch).
- `GET /sessions/{id}/now-playing/lyrics` (`LyricsController`, `[RequireSessionAccess]` only; 404 when nothing plays or no synced lyrics match) returns `{songId, lines:[{timeMs,text}], offsetMs}` for the playing song (`offsetMs` is the saved offset, 0 when none).
- `LyricsService` asks LRCLIB (`PartyKing.Infrastructure.Lyrics`, `ILrclibClient`, keyless, not metered; returns an `Unavailable` result instead of throwing) first by track+artist, then by free text. `LyricsQuery` (Domain, `Entities/Lyrics`) derives title/artist (Spotify: strip remaster/feat suffixes; YouTube: best-effort cleaning and splitting of "Artist - Title"). `LyricsMatcher` takes a synced, non-instrumental candidate whose title matches and whose duration is within 5 s (when known).
- `LyricsCache` (singleton) remembers found lyrics 24 h and misses 1 h per song URL; an unreachable LRCLIB is not cached.

### Position sync

- `POST player/state` also carries `positionMs` and `seeked`. `PlayerStateStore` extrapolates the position in `Get` (stored position plus the time since the last report unless paused), and only a song/pause change or a seek counts as a change, so position adds no broadcasts or polling. `now-playing` and `PlayerStateChanged` carry `positionMs`; the existing 4 s poll re-anchors.
- The host's player detects seeks (Spotify: `player_state_changed` position vs `createPositionTracker`; YouTube: `onSeeked`).
- UI: `useLyrics` (SWR per song); `LyricsView` (250 ms local tick from the anchored position, `lyricsTiming.ts`); a lyrics toggle in `SlideshowControls` that stays enabled and shows a crossed-out icon when the song has no lyrics (mode persisted as `partyking.showLyrics`).

### Shared timing offset

- The host and admins can save the timing offset for a song with the tick button next to the `−` / `+` controls in `LyricsView`. `PUT /sessions/{id}/now-playing/lyrics/offset` (`LyricsController`, `[RequireSessionAccess]`, host or admin through `AuthorizeManagerAsync`, 403 otherwise; 404 when nothing plays, 400 beyond ±100 s) stores `{offsetMs}` in the `LyricsOffsets` table and pushes `LyricsOffsetChanged {songId, offsetMs}` to the saving session's group only.
- The table is keyed by `QueueItem.Url` only, so it is global: every session playing that song uses it (saving 0 stores 0). Another session playing the same song learns of a change on its next lyrics fetch, not by push.
- **Aggregate**: `LyricsOffset : AggregateRoot` (`Domain/Entities/Lyrics`) owns the ±`MaxOffsetMs` (100 s) rule. `Change(offsetMs, sessionId, songId)` returns a `LyricsOffsetFailure` (`OutOfRange`; `Validate` is the same rule for a caller that refuses before looking anything up) and raises `LyricsOffsetChanged(SessionId, SongId, SongUrl, OffsetMs)` (always, also for the saved value).
- `ILyricsOffsetsRepository` is a Domain port (`GetAsync`, `ChangeAsync`). `ChangeAsync` loads or creates the row, runs `Change`, saves and dispatches `DrainEvents()` with `CancellationToken.None`; two saves racing for a new song hit the primary key once and the loser starts again on the winner's row (2 attempts), so both succeed. `LyricsPushHandler` (`Application/Lyrics/Handlers`) sends the `ILyricsNotifier` push; `LyricsService` does not.
- UI: `LyricsView` keeps a local draft that starts at the saved offset of each song. Any viewer (guests too) can nudge it locally, but a saved value (fetched, pushed through `useSessionMusicHub` into the `lyricsKey` SWR cache, or saved by this viewer) overwrites the draft. Nothing is kept in `localStorage`.
- Tests: `LyricsOffsetTests` (Domain), `LyricsOffsetsRepositoryEventsTests`, `LyricsServiceTests` (its fake runs the real aggregate through `TestEvents.LyricsDispatcher`).
