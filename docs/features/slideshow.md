# Slideshow and photos

## Contents

- Flow and in-memory state
- Settings and aggregate
- Photos and rotation
- Upload rules (untrusted input)
- Photo archive (zip)
- Push (SignalR)

## Flow and in-memory state

- `SlideshowService` and `SlideshowImagesRepository` are singletons that hold in-memory state. `SlideshowService` keeps, per session id, the current image and its expiration time (with a per-session lock); the repository keeps only the web root path.
- `Program.cs` awaits `ISlideshowService.InitializeAsync` once at startup, after the migration and admin seed: it creates the `Images/Uploaded` folder under `WebRootPath`, sweeps leftover archives (see below) and removes the old shared `SampleImages` folder (the migration `MadeSlideshowPerSession` deleted its rows).
- `GET /sessions/{id}/photos/current` (`[RequireSessionAccess]`) returns the session's current image, or 204 when there is none (there are no placeholders).
- While a host or admin displays the slideshow (see Push) a server timer advances it at `SlideTime`. Without one, the first read after the expiration advances it, so a client without a live connection still sees it move.
- When an image with `DeleteAfterPresentation` is rotated out, it gets deleted.
- Uploads go to `Images/Uploaded/{sessionId}/`. `SessionAdminService.DeleteAsync` removes that folder and the in-memory slide (`ISlideshowService.DeleteSessionAsync`); the rows go with the cascade delete.
- `ISlideshowService.GetSettingsAsync` / `UpdateSettingsAsync` / `WatchAsync` take the `SessionCaller` and answer Forbidden to anyone but the host or an admin (`RunAsManagerAsync`). `SessionHub.WatchSlideshow` turns that into a `HubException`; `SlideshowController` holds no authorizer.

## Settings

- One `SlideshowSettings` row per session (`SessionId` is the key, cascade delete), edited by the host or an admin through `GET` / `PUT sessions/{id}/slideshow-settings` (`[RequireSessionAccess]`; 403 for anyone else) and shown as `SlideshowSettingsForm` in the session settings dialog.
- A session without a row uses the defaults: `SlideshowSettings:SlideTime` from appsettings, `AutoRepeat` on, `StartFromBeginning` on. The slide time must be 1 s to 1 h. The upload directory still comes from appsettings.
- **Aggregate**: `SlideshowSettings : AggregateRoot` (`Domain/Entities/Slideshow`) owns the defaults (`Defaults(sessionId, defaultSlideTime)`) and the range (`MinSlideTime` / `MaxSlideTime`; `ValidateSlideTime` is the rule for a caller that refuses before looking anything up). `Change(autoRepeat, startFromBeginning, slideTime?, defaultSlideTime)` saves all three (a left-out slide time becomes the default, which is not range-checked), returns a `SlideshowSettingsFailure` and raises `SlideshowSettingsChanged` (always, also for unchanged values).
- `ISlideshowSettingsRepository.ChangeAsync` (implemented by `SlideshowImagesRepository`, which takes the dispatcher) loads or creates the row, runs `Change`, saves and dispatches with `CancellationToken.None`. Two saves racing for a new session hit the primary key once and the loser retries on the winner's row.
- The settings push nothing (the slide only follows with its next poll). `SlideshowSettingsHandler` (`Application/Slideshow/Handlers`) calls `ISlideshowService.RestartSlideAsync`, which restarts the slide on display with the new time. The rotation engine (timers, expiration) stays in `SlideshowService`.
- Tests: `SlideshowSettingsTests` (Domain), `SlideshowSettingsRepositoryEventsTests`, `SlideshowServiceTests` (its fake repository runs the real aggregate through `TestEvents.SlideshowDispatcher`).

## Photos and rotation

- `SlideshowImage` has `internal set` properties (the constructor makes its name and url; `MarkPresented()` is internal).
- `SlideshowPlaylist` (`Domain/Entities/Slideshow`, no table, like `SessionQueue`) holds the rule of which photo comes next. `Advance(currentId, settings)` marks the photo being left as presented (if it is still there) and picks the next in id order (GUIDv7 = upload order, compared as text like the database sorts it). With `StartFromBeginning` every photo is a candidate, otherwise only those not presented. At the end it starts over when `AutoRepeat` is on (a `SlideshowStep` says `Repeated`), else there is none.
- `SlideshowImagesRepository.GetSlideshowImageAsync` loads the session's photo rows tracked in one writer context, runs `Advance` and saves once.
- Tests: `SlideshowPlaylistTests` (Domain), `SlideshowImagesRepositoryTests` (SQLite).

## Upload rules (untrusted input)

`PUT /sessions/{id}/photos/{deleteAfterPresentation}` is open to every guest who knows the session password, so:

- Only jpg/jpeg/png/gif/webp, magic bytes checked, a file capped at 20 MB. `UploadedImageRules` in Domain: `CheckFile(name, length)` and `Detect(header)`, with `UploadedImageFailure`. `UploadedImageValidator` in Application only reads the first 12 bytes, rewinds the stream and words the failures as `InvalidUploadException` messages.
- 100 MB per request via `[RequestSizeLimit]` (Kestrel's default applies elsewhere). The stored extension and content type are those of the *detected* format, never the client's.
- The upload endpoint is rate limited (60/min per IP). Static files are served only for those image types, with `nosniff`. nginx adds a sandbox CSP on `/api/Images/` and raises the body limit to 100 MB for `/api/sessions/<guid>/photos/` (see `../deployment.md`).
- The static image files themselves carry no auth: their URLs hold the session GUID and a GUIDv7 file name.
- UI: the "Upload photos" icon (`AddPhotoAlternate`) in the icon row of `SessionHeader`, next to the games menu, opens `PhotoUploadDialog` (with `PhotoUploader`) on the session page; full screen below 640 px, can't be closed while an upload runs. Its open state is the `?upload=1` query parameter (`useUploadDialog`), so Back closes it and a reload reopens it. A 401 during the upload closes it and the page asks for the password again. The old `/s/:sessionId/upload` route redirects there through `UploadRedirect`.

## Photo archive (zip)

- The host or an admin downloads what is still on disk of a session's uploads as a zip from the "Photos" section of the settings dialog (`PhotoDownload`, a plain `<a download>` link, disabled when there are none; no blob in the browser).
- `SlideshowArchiveService` (`Application/Slideshow/Services`, scoped, separate from the singleton `SlideshowService`; host-or-admin through `RunAsManagerAsync`, 403 otherwise) sits behind `GET sessions/{id}/photos/count` (`{count}`, feeds the link state) and `GET sessions/{id}/photos/archive` (`SlideshowController`, `[RequireSessionAccess]`, rate limit `photo-archive`, 5 per minute per IP; 404 without photos).
- `ISlideshowImagesRepository.CreateSessionArchiveAsync` writes the zip to `{temp}/partyking-archives/<guid>.zip`: entries stored without compression, named `001.ext`, `002.ext`... in file-name order (which is upload order because the names are GUIDv7); a photo rotated out mid-write is skipped; a failed or cancelled build deletes its file. The controller serves it through a `FileStream` with `DeleteOnClose`, so the file goes when the response ends. `InitializeAsync` sweeps leftovers at startup.
- Only zip: RAR can't be written without WinRAR, and 7z was not requested after all. nginx turns `proxy_buffering` off in the `/photos/` location (also the upload one), so the archive streams.
- Tests: `SlideshowArchiveTests`, `PhotoDownload.test.tsx`.

## Push

- `SessionHub` has two methods, `WatchSlideshow` (host or admin only, `HubException` otherwise) and `StopWatchingSlideshow`, which `SlideshowView` calls through the hub connection while it is mounted (a collapsed drawer unmounts it, so it doesn't count). `SlideshowWatchers` (singleton) counts the watching connections per session, and `OnDisconnectedAsync` removes a closed one.
- The first watcher catches up an expired slide once and arms the session's timer (`SlideshowScheduler`, one `Task.Delay` per session, the same shape as `PlayerConnectionWatchdog`). The timer rotates under the per-session lock, re-arms, and pushes the payload-free `SlideChanged` to the whole session group only when the image on display changed; clients then `GET` the current slide, so no image URL goes out in a push.
- The last watcher leaving cancels the timer, so nothing rotates or deletes `DeleteAfterPresentation` images while nobody watches.
- A `SlideTime` change restarts the slide on display with the new time counted from now, and an upload into an empty slideshow rotates at once.
- `useSlideshowWatch` re-sends `WatchSlideshow` after every reconnect (the server forgets it with the old connection). The UI polls the slide every 60 s while the hub is connected and every 5 s otherwise.
