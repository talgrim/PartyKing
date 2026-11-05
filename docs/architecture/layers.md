# Layers and projects

The dependency rules themselves (the "Layering" paragraph, checked by `LayerDependencyTests`) are in `CLAUDE.md`.

## Projects

- **AppHost**: Aspire host. Starts `PartyKing.API` and the UI as an npm app on port 2137.
- **API**: controllers plus `Program.cs`; the composition root, referencing the Infrastructure projects directly. CORS exists in Development only (any origin with credentials). Static files from the web root serve the images (`Images/Uploaded/{sessionId}`, created at startup, not in git).
- **Application**: services, ports, event handlers, mappings, options binding (`SlideshowConfiguration` ← `SlideshowSettings`, `AdminConfiguration` ← `AdminSettings`; the options classes `SpotifyConfiguration`, `LlmConfiguration` are in `Application/Configuration`). References Contract and Domain only.
- **Contract**: public DTOs under `V1/`, returned by controllers.
- **Domain**: entities (`SlideshowImage`, `SlideshowPlaylist`, `SlideshowSettings`, `QueueItem`, `SpotifyProfile`, `Session`, `AdminAccount`, `OAuthState`), enums, read/write models and the rules of the games (`../games/games-lobby.md`). The repository ports of the aggregates (`ISessionsRepository`, `IQueueRepository`, `IAdminAccountsRepository`, `ISlideshowSettingsRepository`, `ILyricsOffsetsRepository`, `IOAuthStatesRepository`, `ILobbyGamesRepository<,>` and the Resistance/Ringo markers) live in `Domain/Repositories`, with their results (`LobbyStatus`, `LobbyResult`, `QueueItemRemoval`). Domain references nothing.
- **Infrastructure** (and `.Spotify`, `.Youtube`, `.Lyrics`, `.Llm`): repositories that wrap EF Core, and the external clients. They implement the ports of Application and Domain, so they reference Application and Domain (Infrastructure also Persistence).
  - `.Spotify` wraps the Spotify Web API client (`ISpotifyIntegrationClient`: auth, search, track lookup); `SpotifyApiExceptionClient` turns the SDK's `APIException` into `SpotifyApiException`.
  - `.Youtube` wraps YouTube's oEmbed endpoint (`IYoutubeOEmbedClient`).
- **Persistence**: EF Core SQLite `DbContext`s, entity configurations, migrations, seed data.
- **ServiceDefaults**: Aspire OpenTelemetry/health/resilience defaults (`AddServiceDefaults`, `MapDefaultEndpoints`).

Each layer registers its own services through a `DependencyInjection.cs` extension (`RegisterApplication`, `RegisterInfrastructure`), and `PartyKing.API/DependencyInjection.cs` chains them.

## Application

### Ports

Application declares the ports it needs in `Application/Ports` (`ISpotifyProfilesRepository`, `ISlideshowImagesRepository`, the clients `ISpotifyIntegrationClient`, `IYoutubeOEmbedClient`, `ILrclibClient`, and `LlmProviderException` / `SpotifyApiException`); Infrastructure implements them. Application never sees the Spotify SDK.

### Never sees HTTP

The services return what happened and the API writes the cookies. `PartyKing.Architecture.Tests` fails CI otherwise.
- `IAdminService.LoginAsync` returns the admin id; `ISpotifyService.AuthenticateByCodeAsync` a `HostLogin` and `AuthenticateByProfileIdAsync` a `SpotifyAccessGrant` (`Application/Spotify/Models`).
- `ISlideshowService.UploadImagesAsync` takes `UploadedFile`s (`Application/Slideshow/Models`: name, length, a way to open the stream), which `SlideshowController` makes from the `IFormFile`s.
- `ISpotifyProfilesRepository.GetProfileAsync` returns `SpotifyProfile?` (the services answer `SpotifyErrors.ProfileNotFound`). `SlideshowImageWriteModel` (it carries a `Stream`) is a port type in `Application/Ports`, so `SlideshowImage` is built from plain values.

### Session services

- `SessionService`: list, get, rename. `SessionAdminService`: create, delete, release. `SessionAccessService`: join, change password, `HasAccessAsync`. `SessionJoinTokenService`: QR tokens. `HostSessionCleanup`: what is forgotten when a host goes.
- `SessionAuthorizer` provides `AuthorizeManagerAsync` and the viewer role; most other services inject it. It is a singleton (it only reads through the singleton sessions repository), so the singleton `SlideshowService` can use it too. Host-or-admin actions run through its `RunAsManagerAsync` extension, which checks first and then runs the action.
- Every one of them takes a `SessionCaller` (host profile id plus admin flag; `GameCaller` extends it), which the API builds in one place with `HttpContext.ReadSessionCallerAsync`.
- The rules themselves are the `Session` aggregate's (`AccessStamp` / `IsUnlockedBy`, `IsHostedBy` / `IsManagedBy`, `Rename`, `Session.Create`) and the `SessionPassword` value object. `Sessions/Helpers` only maps their failures to errors (`SessionNamePolicy`, `SessionPasswordErrors`) and holds `SessionErrors`; `SessionErrors.NotFound` is the one "session was not found" error every service returns.
- **Never do slow work in the constructor of a scoped service.** `SessionAccessService`'s dummy hash for unknown sessions is the lazy singleton `SessionDummyHash`, because the scoped service is built on every guarded request. `AdminService`, which the admin cookie handler builds on every request that reads the caller, takes its dummy hash the same way from the singleton `AdminDummyHash`; hashing in that constructor cost about 55 ms of CPU per game fetch.

## Mappings

- `Application/Mappings` (plus `FullTrackMappings` in `Infrastructure.Spotify`) use Riok.Mapperly, a source generator: static `[Mapper]` partial classes with extension methods (`ToDto`, `ToQueueItem`, `ToDomain`) that fail the build on an unmapped target member (`RequiredMappingStrategy.Target`).
- Computed members go through `[MapPropertyFromSource]` helpers. Enums map by name (`ResistanceEnumMappings` for the Resistance enums), so keep Domain and Contract member names identical.
- The per-caller views that hide secrets (`ResistanceGameViewBuilder`, `RingoService.ToDto`, the lobby DTOs) stay hand-written on purpose: a leak there is a security bug, so no convention-based mapper decides what a caller sees.
- Tests: `ApplicationMappingsTests`, `FullTrackMappingsTests`, `ResistanceEnumMappingsTests`.
