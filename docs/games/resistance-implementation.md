# Resistance: implementation notes

The game rules (Polish) are in `resistance-rules.md` next to this file; card texts and decisions are also in `CLAUDE.local.md`. The shared lobby, `ResistanceOutcome` and the write lock are in `games-lobby.md`; the domain events and pushes are in `../architecture/domain-events-and-realtime.md`.

## Contents

- Model and endpoints
- Services
- Rules snapshot
- Round flow: start, roles, team, vote, missions, restart
- Plot cards (add-on): deal, Opinion Maker, immediate cards, windows
- Presence (TV view)
- Seat takeover
- Views and UI

## Model and endpoints

- `ResistanceGame` (key `SessionId`; `CreatedAt`, `IsOpen`, `StartedAt`, `LeaderPlayerId`, `Phase`, `MissionNumber`, `RejectedTeams`, `Winner`, `Rules`) owns `ResistancePlayer` rows (`Role`, `IsOnTeam`, `Vote`, `MissionCard`, `PassedLeaderWindow`, `IsReclaimable`), `ResistanceMissionResults` and `ResistancePlotCard` rows. `MinPlayers` 5, `MaxPlayers` 10 (a full lobby answers 409 `Resistance.Full`).
- `ResistancePhase`: `SelectingTeam`, `Voting`, `PlayingMission`, `MissionResult`, `GameOver`, the unused `VoteResult` kept for stable numbers (a migration moved games parked in it to `SelectingTeam` with the same leader), and the appended plot-card phases `DealingCards`, `ResolvingCards`, `LeaderWindow`, `ConfidenceWindow`, `PeekWindow`. The lobby is `StartedAt == null`. Phases are stored as ints, so only append.
- Controllers share the route `games/resistance` behind `ResistanceControllerBase`: `ResistanceController` (lobby and lifecycle; it also calls the round service for start and restart), `ResistanceRoundController` (team, vote, mission, continue, window pass), `ResistancePlotCardsController` (everything under `cards/` plus `rules/plot-cards`) and `ResistanceSeatsController`.
- Lobby endpoints:
  - `GET` — host, admin and joined players; guests who unlocked the session observe once the game is open (404 before).
  - `GET lobby` → `{createdAt, isOpen, isStarted, joined}`. `IResistanceGamesRepository.GetLobbyAsync(sessionId, playerId, playerStamp)` projects those fields with one `AsNoTracking` query into a `ResistanceLobbyState` (null without a game) and does not use the shared read, because every tab asks for the lobby on its pushes (it shows as `operation="GetLobbyAsync"` in the repository histogram). `joined` follows `GameLobby.IsPlayer`: the host and admins are not players by role.
  - `POST` create (host or admin, 409 when one exists), `POST open` (admin only, idempotent), `POST join` (`session-join` rate limit), `DELETE players/{id}`, `PUT players/order`, `DELETE` (end).
- Pushes: `ResistanceChanged` (every action) and `ResistanceOpened`; the UI revalidates `resistanceKey` / `resistanceLobbyKey`.

## Services

`Application/Games/Services/Resistance` has three scoped services that match the controllers:

- `IResistanceLobbyService`: get, lobby, create, open, join, rules, plot-cards switch, kick, reorder, end.
- `IResistanceRoundService`: start, restart, team, vote, mission card, continue.
- `IResistancePlotCardService`: deal, play, confirm, take responsibility, take lead, peek, no confidence, pass window.
- `ResistanceActionGate` (composition, not a base class, so it can be tested alone) runs a player or manager action: access check (`RunAsPlayerAsync`: caller must be allowed and a game must exist; `RunAsManagerAsync`: host or admin), the action on the game through `UpdateAsync`, the failure-to-error mapping, and the caller's view.
- `ResistanceErrors` holds the shared errors and `From(outcome)`, one error per `ResistanceFailure`, pinned by the golden table in `ResistanceFailureErrorsTests`. Codes and messages are matched by the UI and tests: keep them stable.

## Rules snapshot

- Role counts, team sizes and required fails per player count are `ResistanceRules` (defaults in the Domain), overridable at runtime through `ResistanceSettings:PlayerCounts` (`IOptionsMonitor`). The validation rule is `ResistanceCountRules.Validate(playerCount)` in the Domain (returns `ResistanceRulesFailure`s); `ResistanceConfigurationValidator` only words the failures for the configuration path; `IResistanceRulesProvider` keeps the last valid rules.
- The setters of `ResistanceRules` stay public because the JSON column is read with System.Text.Json; every new property must also be copied in `ResistanceRules.Clone`.
- A game snapshots the rules in `ResistanceGame.Rules` (JSON column) at creation. `POST rules/refresh` (host or admin, 409 `RulesLocked` once open) re-reads them; the UI's "Refresh rules" button does that before the lobby is opened. `PUT rules/plot-cards` (see below) switches the add-on.

## Round flow

### Start

- `POST start` (host or admin through `AuthorizeManagerAsync`). `ResistanceGame.Start` needs an open game with at least 5 players (409 `NotOpen` / `NotEnoughPlayers` / `AlreadyStarted`), deals the roles with `ResistanceRoles.Deal` (spies are a third of the players, rounded up: 2, 2, 3, 3, 3, 4) and picks a random first leader, all in one save.
- A started game locks its players: `LobbyGame` asks `IsLocked` (`IsStarted`) in join, kick and reorder, and the status is `LobbyStatus.Started` (409 `Resistance.Started`). `ResistanceLobbyDto.isStarted` hides the invite and makes `GameJoin` say the game already started.

### Roles are secret

- `ResistanceGameViewBuilder.Build(game, caller)` (`Application/Mappings`) gives `yourRole` only to a player of a started game and `fellowSpies` (player ids) only to a spy. Observers, including a host or admin who is not playing, get neither; `leaderPlayerId` is public. Pushes stay payload-free, so each client fetches its own view.
- UI: `ResistanceRoleCard` shows a face-down `CardBackIcon` that a tap turns into `SpyCardIcon` / `OperativeCardIcon` (local state, hidden again after a reload); a spy sees the other spies (`SpyMarkerIcon`) only while it is face up. `ResistancePlayers` puts `LeaderTokenIcon` next to the leader. Icons live in `resistance/ResistanceIcons.tsx`; without a `className` they size themselves from the viewBox with viewport-based `clamp()` classes (`DEFAULT_SIZE`), and the vote icons accept `onClick` / `pressed` / `disabled`.

### Team selection

- `MissionNumber` and `ResistancePlayer.IsOnTeam` hold the leader's team; `Rules.TeamSize(playerCount, mission)` reads the snapshot (the UI reads the same numbers from `rules` in the DTO).
- `PUT team` (`{playerIds}`) is leader-only through `AccessAsync` plus `callerPlayerId == LeaderPlayerId` in `ResistanceGame.SelectTeam`: 403 `NotLeader` (also for the host and admins, who get no override); 409 `NotStarted` / `TeamSubmitted`, or `NotSelectingTeam` in a leader window where no team exists yet; 400 `WrongTeamSize` / `UnknownPlayer`. It needs exactly the required number of distinct players and saves them in one go, which locks the team (`IsTeamSubmitted` = anyone `IsOnTeam`).
- There are no drafts on the server: `ResistanceTeamPicker` (in `ResistancePlayerView`, only for the leader of a started game without a team, never in the TV tab) keeps the picks local until "Zgłoś drużynę do głosowania"; it is keyed on mission and rejection count so its picks reset.
- The DTO has `missionNumber`, `requiredTeamSize` and the public `teamPlayerIds`; the boards mark the team ("W drużynie", ring) and the phase line names it.

### Voting

- Submitting the team sets `Voting` and clears old votes. `PUT vote` (`{approve}`) is for joined players only (403 `NotPlayer`, also for a host or admin who did not join; 409 `NotVoting` / `GameOver` / `NotStarted`). A vote can be repeated and is replaced until the last player has voted.
- `ResistanceGame.Vote` tallies in the same save as the last vote (`ResistanceRoles.ResolveVotes`): a strict majority of all players approves, a tie rejects. An approval resets `RejectedTeams`; a rejection increments it and the fifth ends the game (`GameOver`, `Winner = Spy`). An approved team goes to `PlayingMission`.
- A rejection needs no "Dalej": the lead passes to the next player by `Order` at once, the team (`IsOnTeam`) clears and the phase returns to `SelectingTeam` with the same mission. The votes are kept, so everybody sees who voted how until the new leader submits a team (`SelectTeamAsync` clears them).
- `ToDto` hides the votes: until all voted only `votedPlayerIds` (who, not how) and the caller's own `yourVote` are sent; `votes` / `voteApproved` appear once everybody has voted and the phase is not `Voting` (so also while the next leader picks after a rejection); `revealedRoles` only in `GameOver`.
- UI:
  - `ResistanceVotePanel`: the clickable `VoteApproveCardIcon` / `VoteRejectCardIcon` cards, themselves `role="button"` with `aria-pressed` and keyboard activation (no wrapping button); never in the TV tab or for observers, and `yourVote` is blanked for `?tv=1`.
  - `ResistancePlayerMarks` (voted tick, Za/Przeciw, revealed role) on `ResistanceCompactBoard` and the TV seats (`ResistancePlayers` `marks`).
  - `ResistanceVoteCards` on the TV layout, from the moment all voted until the next team is submitted after a rejection or the last mission card is played: one card per voter signed with their name, beside the team cards, which move to the left.
  - The rejection tracker on `ResistanceMissionBoard`.

### Missions

- `PUT mission` (`{success}`) is for team members only: 403 `NotPlayer` / `NotOnTeam`, 403 `MustSucceed` for a Resistance member playing Fail, 409 `AlreadyPlayed` / `NotPlayingMission` / `GameOver`. The card is stored in `ResistancePlayer.MissionCard` and never sent; the DTO has only `playedMissionPlayerIds`, `yourMissionCard` and `requiredFails`.
- The last card resolves the mission in the same save (`ResistanceRoles.ResolveMission`: fails below `Rules.RequiredFails` succeed), appends a `ResistanceMissionResults` row (`missionResults` in the DTO: number, succeeded, fail count) and ends the game at 3 successes or 3 failures. Otherwise the phase is `MissionResult` until "Dalej" (`ContinueAsync`, valid only in `MissionResult`) passes the lead and starts the next mission.
- UI: `ResistanceMissionPanel` in `ResistancePlayerView`; results on `ResistanceMissionBoard`; `ResistanceMissionCards` shows a finished mission's Success/Fail cards without names until "Dalej"; the "Dalej" button is for the leader (`ResistancePlayerView`) and managers (`ResistanceManagerControls`).

### Restart

- `POST restart` (host or admin) sends a finished game back to the open lobby with the same players and rules snapshot. `ResistanceGame.Restart` needs `Phase == GameOver` (409 `NotOver`; 409 `NotStarted` before the start; 404 without a game), clears mission results, votes, team, cards, roles and leader through `ResistanceRoles.Reset` (mission 1, no rejections, no winner, seat releases cleared) and sets `StartedAt` to null.
- The lobby rules apply again: more players can join (the game stays open), plot cards can be switched and "Start game" deals again. `SetPlotCardsAsync` is allowed until the game starts (open or not); refreshing the rules still needs a game that is not open. UI: "Restart game" in `ResistanceManagerControls`, only in `GameOver`.

## Plot cards (add-on)

All nine plot cards are playable.

### Dealing

- `ResistanceRules.PlotCardsEnabled` (default off, kept by `RefreshRulesAsync`) is switched by `PUT rules/plot-cards` (host or admin, 409 `RulesLocked` once open; the "Enable Plot cards" checkbox in the "Game settings" dialog, `ResistanceGameSettings`).
- `PlotDeck` is the 15-card deck; `PlotCardsPerRound` is 1 for 5-6 players, 2 for 7-8, 3 for 9-10. `ResistancePlotCard` rows (`Location` Deck/Drawn/Hand, `DeckOrder`, `HolderPlayerId`) are built and shuffled by `ResistanceRoles.BuildPlotDeck` on `Deal`.
- `ResistanceRoles.BeginMission` draws at the start of each mission (not after a rejected vote) and enters `DealingCards`; `SelectTeamAsync` answers 409 `CardsNotDealt` until the leader deals.
- `PUT cards/deal` (`{assignments:[{cardId,playerId}]}`, leader only) gives every drawn card except Establish Confidence (which stays with the leader) to another player.
- `ToDto` gives `yourPlotCards` only to their holder and public `plotCardCounts` (hand sizes). UI: `ResistancePlotDealer`, `ResistancePlotHand`, the count badge in `ResistancePlayerMarks`; the TV tab blanks `yourPlotCards`.

### Common rules for the windows

`LeaderWindow`, `ConfidenceWindow` and `PeekWindow` share one mechanism:

- A window opens whenever any playable card exists (`ResistanceRoles.HasPlayableCards`: a card in a hand, Opinion Maker excluded), whatever it is, so the window never tells which card is out there and the UI names no card to non-holders.
- Everyone confirms with `PUT leader-window/pass` (`ResistancePlayer.PassedLeaderWindow`; joined players only, 403 `NotPlayer`; 409 `CardNotPlayableNow` outside a window). Nobody learns who holds a card, only that one exists. The public `leaderWindowPassedPlayerIds` says who confirmed; when all did, `AfterWindow` moves on.
- A holder instead plays the card with its own endpoint. UI: `ResistanceLeaderWindow` serves the leader and confidence windows.

### Opinion Maker

- Dealing it puts the card `InPlay` (face up for everyone, auto-played; hand counts exclude it). `ResistanceRoles.OpinionMakerIds` lists the holders. While one has not voted, `VoteAsync` answers 409 `OpinionMakersFirst` to everybody else, and an Opinion Maker's own vote is final (409 `VoteLocked`; two holders vote independently and the others unlock when both have).
- The DTO has public `opinionMakerPlayerIds` and `opinionMakerVotes` (filled once every holder has voted) and `ResistancePlotCardDto.inPlay`. The vote panel waits and locks accordingly; the marks show a "Kreator opinii" badge plus the revealed vote.
- On the TV layout `ResistancePlayers` takes a `beside` slot (a card on the side of the seat facing the board), where `ResistanceTvBoard` shows `PlotOpinionMakerCardIcon` next to each holder; while the phase is `Voting` its "Głosy" section (`ResistanceVoteCards`) already shows `opinionMakerVotes`, the others joining once everybody has voted.

### Immediate cards

Overheard Conversation, Establish Confidence and Open Up (`ResistanceRoles.IsImmediate`) are played at once.

- After the deal `ResistanceRoles.AfterDeal` enters `ResolvingCards` when any is in a hand (else `SelectingTeam`); `SelectTeamAsync` answers 409 `CardsNotDealt` until all are done. They are played in parallel, in any order, and none can be declined.
- `PUT cards/{id}/play` (`{targetPlayerId}`, holder only): a neighbour by `Order` (wrapping) for Overheard Conversation, any other player for the rest (Open Up may target the leader; the leader plays Establish Confidence, which stays in their hand, on someone else). Errors: 403 `NotYourCard`, 404 `UnknownCard`, 409 `CardState` / `NotResolving`, 400 `InvalidTarget`. The card becomes `Played` with `TargetPlayerId`, `PlayedAt` and `PlayedInMission`.
- `PUT cards/{id}/confirm` is for the viewer only (`ResistanceRoles.Viewer`: the holder of Overheard Conversation, otherwise the target); the last confirmation of the round sets `SelectingTeam` in the same save.
- The DTO has a public log `playedPlotCards` (newest first, kept for the whole game, no identity cards in it) and the private `yourReveals` (`ResistanceRoles.Revealed`: the neighbour's role for the holder of Overheard Conversation, the holder's role for the target of the others) until the viewer confirms; the TV tab blanks `yourReveals`.
- UI: `ResistancePlotPlayer` (target picker, "Widziałem/am"); `ResistancePlotLog` under the phase banner (scrolls past 3 rows, `max-h-[4.25rem]`). The host and admins get a "Hide log" / "Show log" button in `ResistanceManagerControls` (phone Controls panel and TV nav alike), shown only once a card was played; the choice is `partyking.resistanceLogHidden` in `localStorage`, read by `Resistance`, which then leaves the log out of the shared `banner`.

### In the Spotlight

- The same `PUT cards/{id}/play`, but only in `ConfidenceWindow` (409 `NotPlayingMission` otherwise). It uses the same single confirmation stop as No Confidence: after an approved vote with any playable card in a hand everyone confirms once, or the holder names the forced player instead. The mission cards answer `NotPlaying` until the window ends.
- The target is another team member (400 `InvalidTarget`), once per mission (409 `CardState`). The card becomes `Played` with the target and `PlayedInMission`, already confirmed, so `UnconfirmedCards` (immediate cards only) ignores it.
- A No Confidence played afterwards cancels the team, so an In the Spotlight played in that window goes back to its holder's hand and out of the log.
- `ResistanceRoles.OpenMissionPlayerId` finds the forced player of the current mission; the DTO has the public `openMissionPlayerId` and, once they played, `openMissionCard`. UI: `ResistanceSpotlightPlayer` in `ResistancePlayerView`; the phase line names the forced player and their open card.

### Take Responsibility

- `PUT cards/{id}/take` (`{targetPlayerId, takeOpinionMaker}`, holder only) is `ResistanceGame.TakeResponsibility`. Errors: 403 `NotYourCard`, 404 `UnknownCard`, 409 `CardState`; 409 `CardNotPlayableNow` in any phase but `SelectingTeam` / `PlayingMission` / `PeekWindow` / `MissionResult` (never mid-vote); 400 `InvalidTarget` for yourself or an unknown player.
- The candidates are the target's face-up Opinion Maker and a random card from their hand. With both, `takeOpinionMaker` must say which (400 `ChoiceRequired`; the UI knows both from the public `opinionMakerPlayerIds` and `plotCardCounts`); with one the server takes it; with none the card is lost.
- The taken card only changes `HolderPlayerId` (an Opinion Maker stays `InPlay`, so `OpinionMakerIds` follows it); the played card is logged with the target, already confirmed. Nobody is told which hand card went; the taker sees it in their own hand. UI: `ResistanceTakeResponsibility`.

### Strong Leader (`LeaderWindow`)

- Opens at the start of a mission before the leader draws (`ResistanceGame.LeaderWindowBeforeDraw`, then `AfterLeaderWindow` draws) and after a rejected team before the new leader picks.
- The holder calls `PUT cards/{id}/take-lead` (403 `NotYourCard`, 404 `UnknownCard`, 409 `CardState` / `CardNotPlayableNow`, 400 `InvalidTarget` for the leader): the card is logged `Played` (target = the ousted leader, already confirmed), the lead moves and the window ends.
- A window cannot reopen without a vote resolving in between, which is the card's "next one only after the next vote" rule.

### No Confidence (`ConfidenceWindow`)

- Opens in `ResolveVotes` when a team is approved while any playable card is in a hand (otherwise the mission starts at once). `RejectedTeams` is not reset until the window ends; when everyone confirmed, `AfterWindow` starts the mission.
- The holder calls `PUT cards/{id}/no-confidence` (`ResistanceGame.PlayNoConfidence`; 403 `NotYourCard`, 404 `UnknownCard`, 409 `CardState` / `CardNotPlayableNow`): the card is logged `Played` (target = the leader whose team was cancelled) and `RejectTeam` runs, the same path as a lost vote (count +1; the fifth ends the game for the spies; else the lead passes, the team clears and a leader window may open).

### Keeping a Close Eye on You (`PeekWindow`)

- The last mission card resolves the mission (the result is public at once). When any playable card is in a hand the phase is `PeekWindow` instead of `MissionResult` and "Dalej" stays hidden; when everyone confirmed, `AfterWindow` sets `MissionResult`.
- The mission cards stay in `ResistancePlayer.MissionCard` until "Dalej". `PUT cards/{id}/peek` (`{targetPlayerId}`, holder only) is `ResistanceGame.PeekMissionCard`. Errors: 403 `NotYourCard`, 404 `UnknownCard`, 409 `CardState`, 409 `WrongPhase` / `CardNotPlayableNow` outside `PeekWindow`; 400 `InvalidTarget` for your own card, a player who played no card, or a card another Keeping a Close Eye already looked at.
- The play is public (the log names holder and target, `Played`, already confirmed); the card seen is the private `yourPeeks` (`ResistanceRoles.PeekedCards`, only in `MissionResult`, blanked in the TV tab).
- A mission that ends the game leaves no window to peek (`GameOver`, 409 `GameOver`), and "Dalej" clears the cards, so a peek must happen before it. UI: `ResistanceCloseEye`.

## Presence (TV view)

The TV shows a dot per seat (green online, grey offline, the seat dimmed) for players who have the Resistance page open.

- `IGamePresence` (`Application/Realtime`, singleton, in memory, generic by `GameKind` but pushing only for Resistance) keeps a set of hub connection ids per `(kind, session, player)`. `ConnectAsync` pushes at once when the player comes online; `Disconnect` keeps them online for a 10 s grace (`GamePresence.DefaultGrace`, a cancelable `Task.Delay` per player, like `PlayerConnectionWatchdog`) before the offline push, so a reconnect or page change does not flicker. Nothing is cleaned up on session delete because every entry dies with its connection.
- `SessionHub` registers a connection only when the URL has `&presence=1` and the signed `cookie.partyKing.resistance` yields a player id (the player is never taken from the client). The TV tab shares that cookie, so `Resistance.tsx` sends the flag only for a view that is not the TV layout and not `?tv=1`. The dev persona reaches the hub through `&persona=`, which `DevPersonaMiddleware` reads on `/hubs/session`.
- The push is `ResistancePresenceChanged` with the whole `{onlinePlayerIds}` (`ResistancePresenceDto`) through `IResistanceNotifier.PresenceChangedAsync`, on its own topic `resistancePresence`, which only the TV layout follows (`RESISTANCE_TV_HUB_TOPICS`), so phones never refetch the game because someone's signal blinked.
- `GET presence` (`ResistancePresenceService`, host or admin) is the snapshot the TV loads and refetches after a reconnect (`useResistancePresence`; no data means no status shown yet).

## Seat takeover (returning to a started game)

A started game locks its players, and the player cookie is the only identity, so a player who lost it (a private tab on an iPhone closed by the system) could not return. The host or an admin releases the seat of an *offline* player and a guest claims it from the join page, without a new name.

- **Domain** (`ResistanceGame.Seats.cs`):
  - `SetSeatReleased(playerId, released, playerOnline)` fails with `NotStarted`, `UnknownSeat`, `ManagerSeat` (the seat of the host or of an admin) or `SeatOnline` (releasing a connected player); taking a release back is always allowed.
  - `ClaimSeat(playerId, playerOnline, newStamp)` fails with `SeatNotReleased` (an unknown, unreleased or manager seat alike) or `SeatOnline`; a claim ends the release and sets the stamp.
  - `ResistancePlayer.IsReclaimable` is the release (cleared by `ResistanceRoles.Reset`). `LobbyPlayer.IsAdminSeat` is set at join from `caller.IsAdmin` whatever name the admin typed (a guest who types "admin" has an ordinary seat). `IsManagerSeat` = host (`ProfileId`) or admin seat. `LobbyPlayer.ClaimStamp` (null until the first takeover; `IsHeldBy(stamp)`) cuts the old device off.
  - These columns exist on `ResistancePlayers` only (migration `ReclaimableSeats`; Ringo ignores them in `RingoPlayerConfiguration`, because Ringo has no secrets and no presence). Events: `ResistanceSeatReleaseChanged`, `ResistanceSeatClaimed`.
- **The stamp**: the player cookie holds `session:player[:stamp]` (`GameSeat(PlayerId, Stamp)` in `Application/Games/Services/Base`; `ISignedCookies.GrantGamePlayer` / `TryReadGamePlayer` take and return it; old cookies without a stamp still read). `GameCaller.PlayerStamp` carries it, and `GameLobby.PlayerOf` (so `IsPlayer`, `AccessAsync`, the views and `JoinAsync`) counts the id only when the stamp is the seat's; `GetLobbyAsync` does the same in its projection. Services read `caller.PlayerId` directly inside `RunAsPlayerAsync` lambdas, which is safe only because `AccessAsync` ran first for every non-manager; a manager's own seat is never released, so its stamp never changes.
- **Presence link**: `SessionHub` registers presence only when `IResistanceSeatService.HoldsSeatAsync` agrees, and `ResistanceSeatClaimedHandler` calls `IGamePresence.ForgetAsync`, so the old tab (still connected) cannot keep the seat online.
- **Service** (`ResistanceSeatService`, scoped): `GetClaimableAsync` (anyone who unlocked the session; released seats with no connected device, empty before the start), `SetReleasedAsync` (host or admin through the gate; presence is read inside the write lock), `ClaimAsync` (returns the new `GameSeat`; the controller grants the cookie) and `HoldsSeatAsync`.
- **Endpoints** (`ResistanceSeatsController`): `GET claimable-seats`, `PUT players/{id}/release` `{released}`, `POST claim` `{playerId}` (rate limit `session-join`).
- **Release stays, availability follows presence**: a released seat whose player is online again is hidden and refused, and becomes claimable again when they drop, without the host acting. A release has no timer and ends only by a claim, taking it back, or a restart. After a server restart presence is empty, so the host can release anyone, but no seat is claimable unless the host released it first, which protects a flaky connection.
- **Host**: `GameLobby.JoinAsync` returns a `GameSeat` and gives the session host their own seat back by `ProfileId` even in a started game (no stamp rotation; the host's cookies prove who they are; also for Ringo).
- **Pushes**: both events send `ResistanceLobbyChanged`, and `ResistanceChanged` through `Succeeded()`.
- **UI**: `ResistanceManagerControls` has "Free a seat" once started (`ResistanceSeatsDialog`: offline players with a Release / Take back button; the seats of the host and admins are not listed; presence from `useResistancePresence(sessionId, open)`). `GameJoin` takes an optional `returnToGame` (`ResistanceJoin` passes it) and for a started game shows the `GameSeatPicker` (seats from `useResistanceClaimableSeats`: SWR key `resistanceClaimableSeatsKey`, revalidated by the lobby pushes and polled every 10 s) and a "Rejoin as the host" button for the host; after a claim it revalidates the lobby and game keys like a join. A tab whose seat was taken over becomes an observer (`GET games/resistance` answers the observer view), and `Resistance` shows "no longer a player" with a link to the join page.

## Views and UI

- Pages: `ResistanceEntry` on the `Games` row shows "Create" or "Resume". `Resistance` (`/s/:sessionId/games/resistance`) is a shell (data, hub, dialogs, nav) over two layouts. `ResistanceJoin` (`.../resistance/join`) and `ResistanceInvite` mirror Ringo's.
- Shared parts of the board: `MissionBoardIcon` for the players who joined (`missionBoardFor`: team sizes for 5-10 players from `CLAUDE.local.md`; the 5-player sizes are dimmed with a notice below 5), `ResistancePlayers` along the left, bottom and right margins (`seatPositions` from `ringoSeats.ts`, like Ringo; the board keeps to the middle 80%), the "Players" dialog (`ResistanceSettings`), an "i" button with the Polish rules (`resistanceRules.ts`) and, for admins, "Open the game". The host and admins get "Start game" (disabled below 5) and lose "Players" and "Open the game" once started. The phase banner (`ResistancePhaseBanner`) is in every view.
- **TV layout** (`ResistanceTvBoard`: board in the middle 80%, players around the edge, manager controls in the nav): used with `?tv=1` (host/admin only; ignored for guests) and by a host or admin who is not playing. In `?tv=1` the page hides the role, the "Ty" badge and the spy markers and blanks `yourPlayerId` / `yourRole` for the phase line, because the TV tab shares the player cookies of the same browser.
- **Phone layout** (everyone else): a joined player (host and admin included) has a `ResistanceViewSwitch` between the player view (`ResistancePlayerView`: role card; the default), the board view (`ResistanceCompactBoard`: full-width mission board with the players as a list, leader crown, "Ty") and the queue view (`ResistanceQueuePanel`). The choice is `partyking.resistanceView` (`player`, `board` or `queue`; `useResistanceView`; the old boolean `partyking.resistanceBoardView` = true still reads as the board) in `localStorage`; the TV tab never reads it. From 1024 px wide (`useMediaQuery`) a player always sees the player view on the left and the switch (Board / Queue only) picks the right column. Observers only get the board. The host and admins also get a collapsed "Controls" button that expands the same manager buttons as the TV nav (`ResistanceManagerControls`) plus an "Open TV view" link (new tab, `?tv=1`; the fallback when the browser blocks the second tab).
- **Phase description**: a line above the board (`resistancePhaseDescription` in `resistance/resistancePhase.ts`, Polish) says what phase the game is in and what to do next, depending on whether the viewer is a manager. It never names the viewer's role, since it stays visible while the card is face up.
- **Queue panel**: `ResistanceQueuePanel` is the session's music inside the game, for everyone who has the switch and, for the host and admins who are not playing (TV layout), behind a "Queue" button in the TV nav that opens a dialog. It shows `NowPlayingLine` (title and artist, or the YouTube video name, no art), the `Queue` form and list (anyone adds a song; `canRemove` for the host and admins) and, for the host and admins, `RemotePlayerControls`. Only the host gets the volume (written to `partyking.volume`, so it reaches `HostPlayer` in the music tab of the same browser). The music still plays in the host's tab on the session page: `OpenGamesMenu` opens games in a new tab so it keeps running, and following an in-tab link away from the session page stops it (the panel then shows "The host's player is not connected.").
