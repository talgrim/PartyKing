---
paths:
  - "src/api/**/*.cs"
---

# Backend naming and style (deduced from the existing code)

Follow what the code already does; these are the patterns it uses consistently.

## Files and namespaces
- File-scoped namespaces; the namespace mirrors the folder (`PartyKing.Application.Sessions.Services`). One top-level type per file, named after it. Exceptions: an interface sits in the file of its implementation (`ISessionService` + `SessionService`), and the events of one area share one file (`QueueEvents.cs`).
- Application and Infrastructure implementations are `internal`; only interfaces, DTOs and Domain types are `public`. Records for events and values are `sealed`.
- Private fields are `_camelCase` and `readonly`, assigned from constructor parameters (no primary constructors for services).

## Suffixes
- Contract: every class ends in `Dto`, requests in `RequestDto` (`RenameSessionRequestDto`), grouped by area under `V1/<Area>`.
- Services `<Thing>Service` behind `I<Thing>Service`, in `Application/<Area>/Services`. Domain-event handlers `<Thing>Handler` in `Application/<Area>/Handlers`, registered with `AddDomainEventHandler<TEvent, THandler>()`.
- Ports: repositories `I<Aggregate>Repository` (Domain/Repositories, or `Application/Ports` for the ones Application needs), implemented in `Infrastructure/Repositories`; metering decorators `Metered<Name>`. Push ports `I<Domain>Notifier` in `Application/Realtime`, SignalR implementations in `API/Hubs/Notifiers`, no-op defaults in `NullNotifiers.cs`. External clients `I<Name>Client`.
- Domain: failures `<Thing>Failure` (enum), outcomes and results `<Thing>Outcome` / `<Thing>Result`, error catalogues `<Thing>Errors`, value-object parsers `<Thing>Parser`, in-memory state `<Thing>Store` / `<Thing>Cache`.
- Domain events are `sealed record`s implementing `IDomainEvent`, named in the past tense (`SongQueued`, `HostReleased`), in `Domain/Events/<Area>` with an XML summary. Every event carries the `SessionId`; none carries a secret.
- Mapperly mappers are static partial `<Area>Mappings` classes with `ToDto` / `ToDomain` / `ToQueueItem` extension methods.
- Options classes: `<Name>Configuration` in `Application/Configuration`, bound from a `<Name>Settings` section.

## Methods
- Async methods end in `Async` and take `CancellationToken cancellationToken` as the last parameter (never `ct`). The caller (`SessionCaller` / `GameCaller`) comes right before it.
- Services return `ErrorOr<T>` (`ErrorOr<Success>` when there is nothing to return); not-found and other shared errors come from the `<Area>Errors` helpers (`SessionErrors.NotFound()`), never ad hoc strings.
- Domain actions are synchronous methods that take time and randomness as arguments and return an outcome or failure instead of throwing.
- A call that is too long for one line wraps one parameter per line, and then uses a block body (see Conventions in `CLAUDE.md`).

## Controllers
- Derive from `CoreController`, take `IHttpContextAccessor` first and pass it to the base. The route is on the action and absolute (`[HttpPut("sessions/{sessionId:guid}/password")]`), not on the class. The folder mirrors the path (`Controllers/Games/Resistance/`), and action names leave out the game (`RingoController.Join`).
- Every session endpoint carries `[RequireSessionAccess]`; add `[ProducesResponseType<T>(StatusCodes.Status200OK)]`. Finish with `result.Match(Ok, Problem)`.
- Controllers translate HTTP (cookies, `IFormFile`, language) to plain values; they hold no rules.

## Tests
- A test class is `<Subject>Tests`; a method describes behaviour as a sentence (`ANameOneCharacterOverTheMaximum_IsTooLong`, `Create_TakesTheIdAndTheTimeItIsGiven_AndTrimsTheName`), with `[Fact]` / `[Theory]`.
- Domain tests use no fakes and no database. Application tests share the fakes in `Fakes.cs` (one per port) and `TestEvents` for real dispatchers; repository tests run on SQLite.

## Migrations
- Named in PascalCase after what changed (`MadeSlideshowPerSession`, `ReclaimableSeats`); target `WriterDbContext`.
