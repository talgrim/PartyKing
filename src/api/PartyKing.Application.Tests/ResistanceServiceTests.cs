using ErrorOr;
using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Admin.Services;
using PartyKing.Application.Games.Services;
using PartyKing.Application.Player.Services;
using PartyKing.Application.Realtime;
using PartyKing.Application.Sessions.Services;
using PartyKing.Contract.V1.Games;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Application.Tests;

public class ResistanceServiceTests
{
    private const string HostId = "host-1";

    private static readonly GameCaller Admin = new(null, true);
    private static readonly GameCaller Host = new(HostId, false);

    private readonly FakeSessionsRepository _sessions = new();
    private readonly FakeResistanceGamesRepository _games = new();
    private readonly FakeSessionMusicNotifier _notifier = new();
    private readonly FakeRulesProvider _rules = new();
    private readonly ResistanceService _service;
    private readonly Guid _id;

    public ResistanceServiceTests()
    {
        var clock = new FakeClock();
        var sessionService = new SessionService(
            _sessions,
            new PasswordHasher(),
            new PlayerStateStore(clock),
            new FakeHostTokenProvider(),
            new FakeSlideshowService(),
            JoinTokens.Create(clock),
            clock,
            new NullSessionMusicNotifier(),
            new NullPlayerConnectionWatchdog(),
            NullLogger<SessionService>.Instance);
        _service = new ResistanceService(sessionService, _sessions, _games, clock, _notifier, _rules);
        _id = _sessions.AddSession(HostId, new PasswordHasher().Hash("secret")).Id;
    }

    [Fact]
    public async Task Guests_CannotCreateOpenKickReorderOrEnd()
    {
        var guest = new GameCaller("guest", false);
        var ct = CancellationToken.None;

        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.CreateAsync(_id, guest, ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.OpenAsync(_id, guest, ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.KickAsync(_id, Guid.NewGuid(), guest, ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.ReorderAsync(_id, [], guest, ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.EndAsync(_id, guest, ct)).FirstError.Type);
    }

    [Fact]
    public async Task Creating_SnapshotsTheConfiguredRules()
    {
        _rules.Rules.PlayerCounts[5] = new ResistanceCountRules(1, [2, 2, 2, 2, 2], [1, 1, 1, 1, 2]);

        var created = await _service.CreateAsync(_id, Host, CancellationToken.None);
        _rules.Rules = ResistanceRules.Default();

        var five = created.Value.Rules.PlayerCounts.Single(x => x.PlayerCount == 5);
        Assert.Equal(1, five.Spies);
        Assert.Equal([1, 1, 1, 1, 2], five.RequiredFails);
        Assert.Equal(1, (await _service.GetAsync(_id, Host, CancellationToken.None)).Value.Rules.PlayerCounts.Single(x => x.PlayerCount == 5).Spies);
    }

    [Fact]
    public async Task RefreshingRules_TakesTheCurrentConfigurationUntilTheGameIsOpen()
    {
        await _service.CreateAsync(_id, Host, CancellationToken.None);
        _rules.Rules.PlayerCounts[5] = new ResistanceCountRules(1, [2, 2, 2, 2, 2], [1, 1, 1, 1, 1]);

        var refreshed = await _service.RefreshRulesAsync(_id, Host, CancellationToken.None);

        Assert.Equal(1, refreshed.Value.Rules.PlayerCounts.Single(x => x.PlayerCount == 5).Spies);

        await _service.OpenAsync(_id, Admin, CancellationToken.None);
        var afterOpen = await _service.RefreshRulesAsync(_id, Host, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.Conflict, afterOpen.FirstError.Type);
    }

    [Fact]
    public async Task PlotCards_AreOffByDefaultAndSwitchedUntilTheGameStarts()
    {
        var created = await _service.CreateAsync(_id, Host, CancellationToken.None);
        Assert.False(created.Value.Rules.PlotCardsEnabled);

        var enabled = await _service.SetPlotCardsAsync(_id, true, Host, CancellationToken.None);
        Assert.True(enabled.Value.Rules.PlotCardsEnabled);

        await _service.OpenAsync(_id, Admin, CancellationToken.None);
        var afterOpen = await _service.SetPlotCardsAsync(_id, false, Host, CancellationToken.None);

        Assert.False(afterOpen.IsError);
        Assert.False((await _service.GetAsync(_id, Host, CancellationToken.None)).Value.Rules.PlotCardsEnabled);
    }

    [Fact]
    public async Task PlotCards_NeedAGameAndAManager()
    {
        var guest = new GameCaller("guest", false);

        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.SetPlotCardsAsync(_id, true, guest, CancellationToken.None)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.NotFound, (await _service.SetPlotCardsAsync(_id, true, Host, CancellationToken.None)).FirstError.Type);
    }

    [Fact]
    public async Task RefreshingRules_KeepsThePlotCardsSwitch()
    {
        await _service.CreateAsync(_id, Host, CancellationToken.None);
        await _service.SetPlotCardsAsync(_id, true, Host, CancellationToken.None);

        var refreshed = await _service.RefreshRulesAsync(_id, Host, CancellationToken.None);

        Assert.True(refreshed.Value.Rules.PlotCardsEnabled);
    }

    [Theory]
    [InlineData(5, 1)]
    [InlineData(6, 1)]
    [InlineData(7, 2)]
    [InlineData(8, 2)]
    [InlineData(9, 3)]
    [InlineData(10, 3)]
    public void PlotCardsPerRound_GrowsWithThePlayerCount(int players, int expected)
    {
        var rules = ResistanceRules.Default();

        Assert.Equal(expected, rules.PlotCardsPerRound(players));
        Assert.Equal(15, rules.PlotDeck.Values.Sum());
    }

    [Fact]
    public async Task RefreshingRules_NeedsAGameAndAManager()
    {
        var guest = new GameCaller("guest", false);

        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.RefreshRulesAsync(_id, guest, CancellationToken.None)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.NotFound, (await _service.RefreshRulesAsync(_id, Host, CancellationToken.None)).FirstError.Type);
    }

    [Fact]
    public async Task Creating_MakesAClosedGameAndOnlyOnce()
    {
        var created = await _service.CreateAsync(_id, Host, CancellationToken.None);
        var again = await _service.CreateAsync(_id, Admin, CancellationToken.None);

        Assert.False(created.IsError);
        Assert.False(created.Value.IsOpen);
        Assert.Empty(created.Value.Players);
        Assert.Equal("Resistance.AlreadyCreated", again.FirstError.Code);
        Assert.Equal([_id], _notifier.ResistanceChanges);
    }

    [Fact]
    public async Task Opening_IsForAdminsOnlyAndPushedOnce()
    {
        await _service.CreateAsync(_id, Host, CancellationToken.None);

        var byHost = await _service.OpenAsync(_id, Host, CancellationToken.None);
        var first = await _service.OpenAsync(_id, Admin, CancellationToken.None);
        var second = await _service.OpenAsync(_id, Admin, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.Forbidden, byHost.FirstError.Type);
        Assert.True(first.Value.IsOpen);
        Assert.True(second.Value.IsOpen);
        Assert.Equal([_id], _notifier.ResistanceOpens);
    }

    [Fact]
    public async Task OpeningWithoutAGame_IsNotFound()
    {
        var result = await _service.OpenAsync(_id, Admin, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task AClosedGame_IsHiddenFromGuestsButNotFromManagers()
    {
        var guest = new GameCaller("guest", false);
        await _service.CreateAsync(_id, Host, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, (await _service.GetAsync(_id, guest, CancellationToken.None)).FirstError.Type);
        Assert.False((await _service.GetAsync(_id, Host, CancellationToken.None)).IsError);
        Assert.False((await _service.GetAsync(_id, Admin, CancellationToken.None)).IsError);

        await _service.OpenAsync(_id, Admin, CancellationToken.None);

        Assert.False((await _service.GetAsync(_id, guest, CancellationToken.None)).IsError);
    }

    [Fact]
    public async Task TheLobby_ShowsWhetherTheGameIsOpenAndTheCallerJoined()
    {
        await OpenGameAsync();
        var player = await JoinAsync("Ola");

        var joined = await _service.GetLobbyAsync(_id, player, CancellationToken.None);
        var stranger = await _service.GetLobbyAsync(_id, new GameCaller("guest", false), CancellationToken.None);

        Assert.True(joined.Value.IsOpen);
        Assert.True(joined.Value.Joined);
        Assert.False(stranger.Value.Joined);
    }

    [Fact]
    public async Task Joining_NeedsAnOpenGame()
    {
        await _service.CreateAsync(_id, Host, CancellationToken.None);

        var result = await _service.JoinAsync(_id, "Ola", new GameCaller("guest", false), CancellationToken.None);

        Assert.Equal("Resistance.NotOpen", result.FirstError.Code);
    }

    [Fact]
    public async Task Joining_NamesAreUniqueIgnoringCaseAndSpaces()
    {
        await OpenGameAsync();
        await JoinAsync("Ola");

        var duplicate = await _service.JoinAsync(_id, "  OLA ", new GameCaller("guest", false), CancellationToken.None);

        Assert.Equal("Resistance.NameTaken", duplicate.FirstError.Code);
    }

    [Fact]
    public async Task Joining_StopsAtTenPlayers()
    {
        await OpenGameAsync();
        for (var i = 0; i < ResistanceGame.MaxPlayers; i++)
        {
            await JoinAsync($"Player {i}");
        }

        var eleventh = await _service.JoinAsync(_id, "Late", new GameCaller("guest", false), CancellationToken.None);
        var game = await _service.GetAsync(_id, Admin, CancellationToken.None);

        Assert.Equal("Resistance.Full", eleventh.FirstError.Code);
        Assert.Equal(ResistanceGame.MaxPlayers, game.Value.Players.Count);
    }

    [Fact]
    public async Task AGuestWhoJoined_WatchesTheGameAndIsMarkedAsAPlayer()
    {
        await OpenGameAsync();
        var player = await JoinAsync("Ola");

        var game = await _service.GetAsync(_id, player, CancellationToken.None);

        Assert.Equal(player.PlayerId, game.Value.YourPlayerId);
        Assert.Equal(["Ola"], game.Value.Players.Select(x => x.Name));
    }

    [Fact]
    public async Task AnObserver_HasNoPlayerId()
    {
        await OpenGameAsync();
        await JoinAsync("Ola");

        var game = await _service.GetAsync(_id, new GameCaller("guest", false), CancellationToken.None);

        Assert.Null(game.Value.YourPlayerId);
    }

    [Fact]
    public async Task TheHostAndAnAdmin_JoinUnderTheirOwnNames()
    {
        await OpenGameAsync();

        await _service.JoinAsync(_id, "ignored", Host, CancellationToken.None);
        await _service.JoinAsync(_id, null, Admin, CancellationToken.None);
        var game = await _service.GetAsync(_id, Admin, CancellationToken.None);

        Assert.Equal(["Host (host)", "admin"], game.Value.Players.Select(x => x.Name));
    }

    [Fact]
    public async Task Kicking_And_Reordering_AreForManagersAndPushed()
    {
        await OpenGameAsync();
        var ola = await JoinAsync("Ola");
        var ala = await JoinAsync("Ala");
        _notifier.ResistanceChanges.Clear();

        var reordered = await _service.ReorderAsync(_id, [ala.PlayerId!.Value, ola.PlayerId!.Value], Host, CancellationToken.None);
        var mismatch = await _service.ReorderAsync(_id, [ala.PlayerId.Value], Host, CancellationToken.None);
        var kicked = await _service.KickAsync(_id, ala.PlayerId.Value, Admin, CancellationToken.None);
        var missing = await _service.KickAsync(_id, Guid.NewGuid(), Admin, CancellationToken.None);

        Assert.Equal(["Ala", "Ola"], reordered.Value.Players.Select(x => x.Name));
        Assert.Equal("Resistance.OrderMismatch", mismatch.FirstError.Code);
        Assert.Equal(["Ola"], kicked.Value.Players.Select(x => x.Name));
        Assert.Equal("Resistance.PlayerNotFound", missing.FirstError.Code);
        Assert.Equal(2, _notifier.ResistanceChanges.Count);
    }

    [Fact]
    public async Task Ending_DeletesTheGameAndIsPushed()
    {
        await _service.CreateAsync(_id, Host, CancellationToken.None);
        _notifier.ResistanceChanges.Clear();

        var ended = await _service.EndAsync(_id, Host, CancellationToken.None);
        var again = await _service.EndAsync(_id, Host, CancellationToken.None);

        Assert.False(ended.IsError);
        Assert.Equal(ErrorOr.ErrorType.NotFound, again.FirstError.Type);
        Assert.Equal([_id], _notifier.ResistanceChanges);
    }

    [Fact]
    public async Task Starting_IsForManagersOfAnOpenGameWithFivePlayers()
    {
        var ct = CancellationToken.None;
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.StartAsync(_id, new GameCaller("guest", false), ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.NotFound, (await _service.StartAsync(_id, Host, ct)).FirstError.Type);

        await _service.CreateAsync(_id, Host, ct);
        Assert.Equal("Resistance.NotOpen", (await _service.StartAsync(_id, Host, ct)).FirstError.Code);

        await _service.OpenAsync(_id, Admin, ct);
        await JoinPlayersAsync(4);
        Assert.Equal("Resistance.NotEnoughPlayers", (await _service.StartAsync(_id, Host, ct)).FirstError.Code);

        await JoinAsync("Player 5");
        var started = await _service.StartAsync(_id, Admin, ct);

        Assert.NotNull(started.Value.StartedAt);
        Assert.Contains(started.Value.Players, x => x.Id == started.Value.LeaderPlayerId);
        Assert.Equal("Resistance.AlreadyStarted", (await _service.StartAsync(_id, Host, ct)).FirstError.Code);
    }

    [Fact]
    public async Task Starting_IsPushedOnce()
    {
        await OpenGameAsync();
        await JoinPlayersAsync(5);
        var before = _notifier.ResistanceChanges.Count;

        await _service.StartAsync(_id, Host, CancellationToken.None);

        Assert.Equal(before + 1, _notifier.ResistanceChanges.Count);
    }

    [Theory]
    [InlineData(5, 1, 2)]
    [InlineData(5, 3, 2)]
    [InlineData(6, 3, 4)]
    [InlineData(7, 4, 4)]
    [InlineData(8, 1, 3)]
    [InlineData(10, 5, 5)]
    public void TeamSize_FollowsTheMissionTable(int players, int mission, int size)
    {
        Assert.Equal(size, ResistanceRules.Default().TeamSize(players, mission));
    }

    [Fact]
    public async Task AfterStarting_TheDtoTellsTheRequiredTeamSizeAndHasNoTeam()
    {
        await OpenGameAsync();
        var callers = await JoinPlayersAsync(8);
        var started = await _service.StartAsync(_id, Host, CancellationToken.None);

        Assert.Equal(1, started.Value.MissionNumber);
        Assert.Equal(3, started.Value.RequiredTeamSize);
        Assert.Empty(started.Value.TeamPlayerIds);
        Assert.Equal(8, callers.Count);
    }

    [Fact]
    public async Task SelectingATeam_IsOnlyForTheLeaderAndNeedsAStartedGame()
    {
        var ct = CancellationToken.None;
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.SelectTeamAsync(_id, [], new GameCaller("guest", false), ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.NotFound, (await _service.SelectTeamAsync(_id, [], Host, ct)).FirstError.Type);

        var callers = await OpenGameAndJoinAsync(5);
        Assert.Equal("Resistance.NotStarted", (await _service.SelectTeamAsync(_id, [], Host, ct)).FirstError.Code);

        var started = await _service.StartAsync(_id, Host, ct);
        var ids = started.Value.Players.Select(x => x.Id).ToList();
        var notLeader = callers.First(x => x.PlayerId != started.Value.LeaderPlayerId);

        var denied = await _service.SelectTeamAsync(_id, ids.Take(2).ToList(), notLeader, ct);
        var manager = await _service.SelectTeamAsync(_id, ids.Take(2).ToList(), Admin, ct);

        Assert.Equal("Resistance.NotLeader", denied.FirstError.Code);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, denied.FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, manager.FirstError.Type);
    }

    [Fact]
    public async Task SelectingATeam_NeedsExactlyTheRequiredNumberOfDistinctPlayers()
    {
        var ct = CancellationToken.None;
        var callers = await OpenGameAndJoinAsync(5);
        var started = await _service.StartAsync(_id, Host, ct);
        var leader = callers.First(x => x.PlayerId == started.Value.LeaderPlayerId);
        var ids = started.Value.Players.Select(x => x.Id).ToList();

        Assert.Equal("Resistance.WrongTeamSize", (await _service.SelectTeamAsync(_id, ids.Take(1).ToList(), leader, ct)).FirstError.Code);
        Assert.Equal("Resistance.WrongTeamSize", (await _service.SelectTeamAsync(_id, ids.Take(3).ToList(), leader, ct)).FirstError.Code);
        Assert.Equal("Resistance.WrongTeamSize", (await _service.SelectTeamAsync(_id, [ids[0], ids[0]], leader, ct)).FirstError.Code);
        Assert.Equal("Resistance.WrongTeamSize", (await _service.SelectTeamAsync(_id, null, leader, ct)).FirstError.Code);
        Assert.Equal("Resistance.UnknownPlayer", (await _service.SelectTeamAsync(_id, [ids[0], Guid.NewGuid()], leader, ct)).FirstError.Code);
    }

    [Fact]
    public async Task PlotCards_AreDrawnForTheLeaderAndDealtToOthers_HandsStayPrivateAndCountsPublic()
    {
        var ct = CancellationToken.None;
        await _service.CreateAsync(_id, Host, ct);
        await _service.SetPlotCardsAsync(_id, true, Host, ct);
        await _service.OpenAsync(_id, Admin, ct);
        var callers = await JoinPlayersAsync(5);
        var started = await _service.StartAsync(_id, Host, ct);
        var leader = callers.First(x => x.PlayerId == started.Value.LeaderPlayerId);
        var others = callers.Where(x => x.PlayerId != leader.PlayerId).ToList();

        Assert.Equal(ResistancePhaseDto.DealingCards, started.Value.Phase);
        Assert.Equal(1, started.Value.Rules.PlotCardsPerRound);
        var drawn = (await _service.GetAsync(_id, leader, ct)).Value.YourPlotCards;
        Assert.Single(drawn);
        foreach (var other in others)
        {
            Assert.Empty((await _service.GetAsync(_id, other, ct)).Value.YourPlotCards);
        }

        Assert.Equal("Resistance.CardsNotDealt", (await _service.SelectTeamAsync(_id, [leader.PlayerId!.Value], leader, ct)).FirstError.Code);
        Assert.Equal("Resistance.NotLeader", (await _service.DealPlotCardsAsync(_id, [], others[0], ct)).FirstError.Code);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.DealPlotCardsAsync(_id, [], Host, ct)).FirstError.Type);

        var receiver = others[0];
        var deal = drawn.Where(x => x.ToDeal).Select(x => new ResistancePlotCardAssignmentDto(x.Id, receiver.PlayerId!.Value)).ToList();
        var before = _notifier.ResistanceChanges.Count;
        var dealt = await _service.DealPlotCardsAsync(_id, deal, leader, ct);

        Assert.Equal(ResistancePhaseDto.SelectingTeam, dealt.Value.Phase);
        Assert.Equal(before + 1, _notifier.ResistanceChanges.Count);
        Assert.Equal(1, dealt.Value.PlotCardCounts.Sum(x => x.Count));
        Assert.Equal(deal.Count, (await _service.GetAsync(_id, receiver, ct)).Value.YourPlotCards.Count);
        foreach (var other in others.Skip(1))
        {
            Assert.Empty((await _service.GetAsync(_id, other, ct)).Value.YourPlotCards);
        }

        Assert.Equal("Resistance.NotDealing", (await _service.DealPlotCardsAsync(_id, [], leader, ct)).FirstError.Code);
    }

    [Theory]
    [InlineData(ResistancePlayCardStatus.NoGame, "Resistance.NotFound")]
    [InlineData(ResistancePlayCardStatus.NotStarted, "Resistance.NotStarted")]
    [InlineData(ResistancePlayCardStatus.GameOver, "Resistance.GameOver")]
    [InlineData(ResistancePlayCardStatus.NotResolving, "Resistance.NotResolving")]
    [InlineData(ResistancePlayCardStatus.UnknownCard, "Resistance.UnknownCard")]
    [InlineData(ResistancePlayCardStatus.NotAllowed, "Resistance.NotYourCard")]
    [InlineData(ResistancePlayCardStatus.WrongState, "Resistance.CardState")]
    [InlineData(ResistancePlayCardStatus.InvalidTarget, "Resistance.InvalidTarget")]
    [InlineData(ResistancePlayCardStatus.WrongPhase, "Resistance.CardNotPlayableNow")]
    [InlineData(ResistancePlayCardStatus.ChoiceRequired, "Resistance.ChoiceRequired")]
    public async Task PlayingAndConfirmingAPlotCard_MapTheRepositoryStatusToAnError(ResistancePlayCardStatus status, string code)
    {
        var ct = CancellationToken.None;
        var callers = await OpenGameAndJoinAsync(5);
        await _service.StartAsync(_id, Host, ct);
        _games.PlayCardStatus = status;

        var played = await _service.PlayPlotCardAsync(_id, 0, callers[1].PlayerId!.Value, callers[0], ct);
        var confirmed = await _service.ConfirmPlotCardAsync(_id, 0, callers[0], ct);

        Assert.Equal(code, played.FirstError.Code);
        Assert.Equal(code, confirmed.FirstError.Code);
    }

    [Fact]
    public async Task APlayedPlotCard_IsLoggedForEveryoneButItsIdentityCardOnlyShowsToItsViewer()
    {
        var ct = CancellationToken.None;
        var callers = await OpenGameAndJoinAsync(5);
        await _service.StartAsync(_id, Host, ct);
        var game = (await _games.GetAsync(_id, ct))!;
        var players = game.Players.OrderBy(x => x.Order).ToList();
        var (holder, target, bystander) = (players[0], players[1], players[2]);
        game.Phase = ResistancePhase.ResolvingCards;
        game.PlotCards.Add(new ResistancePlotCard
        {
            SessionId = _id,
            CardId = 0,
            Type = ResistancePlotCardType.OpenUp,
            Location = ResistancePlotCardLocation.Played,
            HolderPlayerId = holder.PlayerId,
            TargetPlayerId = target.PlayerId,
            PlayedAt = DateTimeOffset.UtcNow,
            PlayedInMission = 1,
        });
        game.PlotCards.Add(new ResistancePlotCard
        {
            SessionId = _id,
            CardId = 1,
            Type = ResistancePlotCardType.OverheardConversation,
            Location = ResistancePlotCardLocation.Played,
            HolderPlayerId = holder.PlayerId,
            TargetPlayerId = bystander.PlayerId,
            PlayedAt = DateTimeOffset.UtcNow.AddSeconds(1),
            PlayedInMission = 1,
        });
        GameCaller CallerOf(ResistancePlayer p) => callers.First(x => x.PlayerId == p.PlayerId);

        var asHolder = (await _service.GetAsync(_id, CallerOf(holder), ct)).Value;
        var asTarget = (await _service.GetAsync(_id, CallerOf(target), ct)).Value;
        var asBystander = (await _service.GetAsync(_id, CallerOf(bystander), ct)).Value;
        var asHost = (await _service.GetAsync(_id, Host, ct)).Value;

        // Open Up shows the holder's identity to the target; Overheard Conversation shows the neighbour's to the holder
        Assert.Equal((0, (ResistanceRoleDto)holder.Role!.Value), (asTarget.YourReveals.Single().CardId, asTarget.YourReveals.Single().Role));
        Assert.Equal(holder.PlayerId, asTarget.YourReveals.Single().PlayerId);
        var forHolder = Assert.Single(asHolder.YourReveals);
        Assert.Equal((1, bystander.PlayerId, (ResistanceRoleDto)bystander.Role!.Value), (forHolder.CardId, forHolder.PlayerId, forHolder.Role));
        Assert.Empty(asBystander.YourReveals);
        Assert.Empty(asHost.YourReveals);

        // Both plays are public, newest first, and neither is in anyone's hand
        foreach (var view in new[] { asHolder, asTarget, asBystander, asHost })
        {
            Assert.Equal([1, 0], view.PlayedPlotCards.Select(x => x.Id));
            Assert.Empty(view.YourPlotCards);
        }

        // Once confirmed the identity card is not sent any more, but the log stays
        game.PlotCards.ForEach(x => x.IsConfirmed = true);
        var after = (await _service.GetAsync(_id, CallerOf(target), ct)).Value;
        Assert.Empty(after.YourReveals);
        Assert.All(after.PlayedPlotCards, x => Assert.True(x.IsConfirmed));
    }

    [Fact]
    public async Task APlayedCardWithoutATarget_IsLoggedAndShowsNobodyAnIdentityCard()
    {
        var ct = CancellationToken.None;
        var callers = await OpenGameAndJoinAsync(5);
        await _service.StartAsync(_id, Host, ct);
        var game = (await _games.GetAsync(_id, ct))!;
        var holder = game.Players.OrderBy(x => x.Order).First();
        game.PlotCards.Add(new ResistancePlotCard
        {
            SessionId = _id,
            CardId = 0,
            Type = ResistancePlotCardType.NoConfidence,
            Location = ResistancePlotCardLocation.Played,
            HolderPlayerId = holder.PlayerId,
            PlayedAt = DateTimeOffset.UtcNow,
            PlayedInMission = 1,
        });

        var view = (await _service.GetAsync(_id, callers.First(x => x.PlayerId == holder.PlayerId), ct)).Value;

        var logged = Assert.Single(view.PlayedPlotCards);
        Assert.Null(logged.TargetPlayerId);
        Assert.Empty(view.YourReveals);
        Assert.Empty(ResistanceRoles.UnconfirmedCards(game));
    }

    [Fact]
    public async Task InTheSpotlight_MakesTheTargetsMissionCardPublicOnceTheyPlayedIt()
    {
        var ct = CancellationToken.None;
        var callers = await OpenGameAndJoinAsync(5);
        await _service.StartAsync(_id, Host, ct);
        var game = (await _games.GetAsync(_id, ct))!;
        var players = game.Players.OrderBy(x => x.Order).ToList();
        game.Phase = ResistancePhase.PlayingMission;
        players[1].IsOnTeam = true;
        game.PlotCards.Add(new ResistancePlotCard
        {
            SessionId = _id,
            CardId = 0,
            Type = ResistancePlotCardType.InTheSpotlight,
            Location = ResistancePlotCardLocation.Played,
            HolderPlayerId = players[0].PlayerId,
            TargetPlayerId = players[1].PlayerId,
            PlayedAt = DateTimeOffset.UtcNow,
            PlayedInMission = game.MissionNumber,
            IsConfirmed = true,
        });
        var bystander = callers.First(x => x.PlayerId == players[2].PlayerId);

        var before = (await _service.GetAsync(_id, bystander, ct)).Value;
        players[1].MissionCard = false;
        var after = (await _service.GetAsync(_id, bystander, ct)).Value;

        Assert.Equal(players[1].PlayerId, before.OpenMissionPlayerId);
        Assert.Null(before.OpenMissionCard);
        Assert.Equal(false, after.OpenMissionCard);
        Assert.Null(after.YourMissionCard);
    }

    [Fact]
    public async Task WithoutThePlotCards_TheGameShowsNoCardsAndPicksAtOnce()
    {
        var ct = CancellationToken.None;
        var callers = await OpenGameAndJoinAsync(5);
        var started = await _service.StartAsync(_id, Host, ct);
        var leader = callers.First(x => x.PlayerId == started.Value.LeaderPlayerId);

        Assert.Equal(ResistancePhaseDto.SelectingTeam, started.Value.Phase);
        Assert.Empty(started.Value.PlotCardCounts);
        Assert.Empty((await _service.GetAsync(_id, leader, ct)).Value.YourPlotCards);
        Assert.Equal("Resistance.NotDealing", (await _service.DealPlotCardsAsync(_id, [], leader, ct)).FirstError.Code);
    }

    [Fact]
    public async Task SelectingATeam_LetsTheLeaderPickThemselvesAndLocksTheTeam()
    {
        var ct = CancellationToken.None;
        var callers = await OpenGameAndJoinAsync(5);
        var started = await _service.StartAsync(_id, Host, ct);
        var leader = callers.First(x => x.PlayerId == started.Value.LeaderPlayerId);
        var other = started.Value.Players.First(x => x.Id != leader.PlayerId).Id;
        var before = _notifier.ResistanceChanges.Count;

        var result = await _service.SelectTeamAsync(_id, [leader.PlayerId!.Value, other], leader, ct);

        Assert.False(result.IsError);
        Assert.Equal(2, result.Value.TeamPlayerIds.Count);
        Assert.Contains(leader.PlayerId!.Value, result.Value.TeamPlayerIds);
        Assert.Contains(other, result.Value.TeamPlayerIds);
        Assert.Equal(before + 1, _notifier.ResistanceChanges.Count);
        Assert.Equal("Resistance.TeamSubmitted", (await _service.SelectTeamAsync(_id, [leader.PlayerId.Value, other], leader, ct)).FirstError.Code);
    }

    [Fact]
    public async Task SelectedTeam_IsVisibleToEveryone()
    {
        var ct = CancellationToken.None;
        var callers = await OpenGameAndJoinAsync(5);
        var started = await _service.StartAsync(_id, Host, ct);
        var leader = callers.First(x => x.PlayerId == started.Value.LeaderPlayerId);
        var team = started.Value.Players.Take(2).Select(x => x.Id).ToList();
        await _service.SelectTeamAsync(_id, team, leader, ct);

        var asOther = await _service.GetAsync(_id, callers.First(x => x.PlayerId != leader.PlayerId), ct);
        var asHost = await _service.GetAsync(_id, Host, ct);

        Assert.Equal(team.Order(), asOther.Value.TeamPlayerIds.Order());
        Assert.Equal(team.Order(), asHost.Value.TeamPlayerIds.Order());
    }

    [Fact]
    public async Task Voting_NeedsASubmittedTeamAndAPlayerOfTheGame()
    {
        var ct = CancellationToken.None;
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.VoteAsync(_id, true, new GameCaller("guest", false), ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.NotFound, (await _service.VoteAsync(_id, true, Host, ct)).FirstError.Type);

        var callers = await OpenGameAndJoinAsync(5);
        Assert.Equal("Resistance.NotStarted", (await _service.VoteAsync(_id, true, callers[0], ct)).FirstError.Code);

        var started = await _service.StartAsync(_id, Host, ct);
        Assert.Equal("Resistance.NotVoting", (await _service.VoteAsync(_id, true, callers[0], ct)).FirstError.Code);

        var leader = callers.First(x => x.PlayerId == started.Value.LeaderPlayerId);
        await _service.SelectTeamAsync(_id, started.Value.Players.Take(2).Select(x => x.Id).ToList(), leader, ct);

        var watcher = await _service.VoteAsync(_id, true, Host, ct);
        Assert.Equal("Resistance.NotPlayer", watcher.FirstError.Code);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, watcher.FirstError.Type);
        Assert.Equal("Resistance.NotPlayer", (await _service.VoteAsync(_id, true, Admin, ct)).FirstError.Code);
        Assert.False((await _service.VoteAsync(_id, true, callers[0], ct)).IsError);
    }

    [Fact]
    public async Task Votes_AreSecretUntilEveryoneVoted()
    {
        var ct = CancellationToken.None;
        var (callers, _) = await StartWithTeamAsync(5);
        var before = _notifier.ResistanceChanges.Count;

        await _service.VoteAsync(_id, true, callers[0], ct);
        await _service.VoteAsync(_id, false, callers[1], ct);

        var asVoter = (await _service.GetAsync(_id, callers[0], ct)).Value;
        var asOther = (await _service.GetAsync(_id, callers[2], ct)).Value;
        var asHost = (await _service.GetAsync(_id, Host, ct)).Value;

        Assert.Equal(before + 2, _notifier.ResistanceChanges.Count);
        Assert.Equal(ResistancePhaseDto.Voting, asHost.Phase);
        Assert.True(asVoter.YourVote);
        Assert.Null(asOther.YourVote);
        Assert.Null(asHost.YourVote);
        foreach (var view in new[] { asVoter, asOther, asHost })
        {
            Assert.Equal(2, view.VotedPlayerIds.Count);
            Assert.Null(view.Votes);
            Assert.Null(view.VoteApproved);
        }
    }

    [Fact]
    public async Task AVote_CanBeChangedUntilTheLastPlayerVoted()
    {
        var ct = CancellationToken.None;
        var (callers, _) = await StartWithTeamAsync(5);

        await _service.VoteAsync(_id, true, callers[0], ct);
        var changed = await _service.VoteAsync(_id, false, callers[0], ct);
        Assert.False(changed.Value.YourVote);
        Assert.Single(changed.Value.VotedPlayerIds);

        foreach (var caller in callers.Skip(1))
        {
            await _service.VoteAsync(_id, true, caller, ct);
        }

        var late = await _service.VoteAsync(_id, true, callers[0], ct);
        Assert.Equal("Resistance.NotVoting", late.FirstError.Code);
    }

    [Fact]
    public async Task TheLastVote_RevealsEveryVoteToEveryone()
    {
        var ct = CancellationToken.None;
        var (callers, _) = await StartWithTeamAsync(5);

        await VoteAsync(callers, true, true, true, false, false);

        foreach (var viewer in new[] { callers[3], Host, Admin })
        {
            var game = (await _service.GetAsync(_id, viewer, ct)).Value;
            Assert.Equal(ResistancePhaseDto.PlayingMission, game.Phase);
            Assert.True(game.VoteApproved);
            Assert.Equal(5, game.Votes!.Count);
            Assert.Equal(3, game.Votes.Count(x => x.Approve));
            Assert.Equal(0, game.RejectedTeams);
        }
    }

    [Fact]
    public async Task AnApprovedTeam_PlaysItsMission_AndCannotBeContinuedFromUntilItIsOver()
    {
        var ct = CancellationToken.None;
        var (callers, leader) = await StartWithTeamAsync(5);
        await VoteAsync(callers, true, true, true, true, true);

        var result = await _service.ContinueAsync(_id, leader, ct);

        Assert.Equal("Resistance.NothingToContinue", result.FirstError.Code);
        var game = (await _service.GetAsync(_id, Host, ct)).Value;
        Assert.Equal(2, game.TeamPlayerIds.Count);
        Assert.Equal(ResistancePhaseDto.PlayingMission, game.Phase);
        Assert.Equal(1, game.RequiredFails);
        Assert.Empty(game.PlayedMissionPlayerIds);
        Assert.Empty(game.MissionResults);
    }

    [Fact]
    public async Task MissionCards_NeedAPlayerOnTheApprovedTeam()
    {
        var ct = CancellationToken.None;
        var (callers, leader) = await StartWithTeamAsync(5);
        Assert.Equal("Resistance.NotPlayingMission", (await _service.PlayMissionCardAsync(_id, true, leader, ct)).FirstError.Code);
        await VoteAsync(callers, true, true, true, true, true);

        var team = (await _service.GetAsync(_id, Host, ct)).Value.TeamPlayerIds;
        var outsider = callers.First(x => !team.Contains(x.PlayerId!.Value));

        Assert.Equal("Resistance.NotPlayer", (await _service.PlayMissionCardAsync(_id, true, Host, ct)).FirstError.Code);
        Assert.Equal("Resistance.NotPlayer", (await _service.PlayMissionCardAsync(_id, true, Admin, ct)).FirstError.Code);
        var notOnTeam = await _service.PlayMissionCardAsync(_id, true, outsider, ct);
        Assert.Equal("Resistance.NotOnTeam", notOnTeam.FirstError.Code);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, notOnTeam.FirstError.Type);
    }

    [Fact]
    public async Task AMemberOfTheResistance_CanOnlyPlaySuccess_AndEachCardIsPlayedOnce()
    {
        var ct = CancellationToken.None;
        var mission = await StartMissionAsync(5, spiesOnTeam: 1);

        var failed = await _service.PlayMissionCardAsync(_id, false, mission.Resistance[0], ct);
        Assert.Equal("Resistance.MustSucceed", failed.FirstError.Code);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, failed.FirstError.Type);

        var played = await _service.PlayMissionCardAsync(_id, true, mission.Resistance[0], ct);
        Assert.False(played.IsError);
        Assert.Equal("Resistance.AlreadyPlayed", (await _service.PlayMissionCardAsync(_id, true, mission.Resistance[0], ct)).FirstError.Code);
        Assert.False((await _service.PlayMissionCardAsync(_id, false, mission.Spies[0], ct)).IsError);
    }

    [Fact]
    public async Task TheCards_AreSecret_OnlyWhoPlayedIsPublic()
    {
        var ct = CancellationToken.None;
        var mission = await StartMissionAsync(5, spiesOnTeam: 1);
        var before = _notifier.ResistanceChanges.Count;

        await _service.PlayMissionCardAsync(_id, false, mission.Spies[0], ct);

        Assert.Equal(before + 1, _notifier.ResistanceChanges.Count);
        var asSpy = (await _service.GetAsync(_id, mission.Spies[0], ct)).Value;
        var asOther = (await _service.GetAsync(_id, mission.Resistance[0], ct)).Value;
        var asHost = (await _service.GetAsync(_id, Host, ct)).Value;

        Assert.False(asSpy.YourMissionCard);
        Assert.Null(asOther.YourMissionCard);
        Assert.Null(asHost.YourMissionCard);
        foreach (var view in new[] { asSpy, asOther, asHost })
        {
            Assert.Equal([mission.Spies[0].PlayerId!.Value], view.PlayedMissionPlayerIds);
            Assert.Equal(ResistancePhaseDto.PlayingMission, view.Phase);
            Assert.Empty(view.MissionResults);
        }
    }

    [Fact]
    public async Task TheLastCard_ResolvesTheMission_AndOnlyTheFailCountIsPublic()
    {
        var ct = CancellationToken.None;
        var mission = await StartMissionAsync(5, spiesOnTeam: 1);

        await _service.PlayMissionCardAsync(_id, true, mission.Resistance[0], ct);
        await _service.PlayMissionCardAsync(_id, false, mission.Spies[0], ct);

        var game = (await _service.GetAsync(_id, Host, ct)).Value;
        Assert.Equal(ResistancePhaseDto.MissionResult, game.Phase);
        var result = Assert.Single(game.MissionResults);
        Assert.Equal((1, false, 1), (result.MissionNumber, result.Succeeded, result.FailCount));
        Assert.NotEmpty(result.TeamPlayerIds);
        Assert.Equal(game.TeamPlayerIds, result.TeamPlayerIds);
        Assert.Equal(1, game.MissionNumber);
    }

    [Fact]
    public async Task AMissionWithoutFailCards_Succeeds()
    {
        var ct = CancellationToken.None;
        var mission = await StartMissionAsync(5, spiesOnTeam: 1);

        foreach (var caller in mission.Team)
        {
            await _service.PlayMissionCardAsync(_id, true, caller, ct);
        }

        var game = (await _service.GetAsync(_id, Host, ct)).Value;
        var result = Assert.Single(game.MissionResults);
        Assert.Equal((1, true, 0), (result.MissionNumber, result.Succeeded, result.FailCount));
        Assert.NotEmpty(result.TeamPlayerIds);
        Assert.Equal(game.TeamPlayerIds, result.TeamPlayerIds);
    }

    [Fact]
    public async Task TheRequiredFails_ComeFromTheRulesOfTheGame()
    {
        var ct = CancellationToken.None;
        _rules.Rules.PlayerCounts[5] = new ResistanceCountRules(2, [2, 3, 2, 3, 3], [2, 1, 1, 1, 1]);
        var mission = await StartMissionAsync(5, spiesOnTeam: 1);
        Assert.Equal(2, (await _service.GetAsync(_id, Host, ct)).Value.RequiredFails);

        await _service.PlayMissionCardAsync(_id, true, mission.Resistance[0], ct);
        await _service.PlayMissionCardAsync(_id, false, mission.Spies[0], ct);

        var game = (await _service.GetAsync(_id, Host, ct)).Value;
        var result = Assert.Single(game.MissionResults);
        Assert.Equal((1, true, 1), (result.MissionNumber, result.Succeeded, result.FailCount));
        Assert.NotEmpty(result.TeamPlayerIds);
        Assert.Equal(game.TeamPlayerIds, result.TeamPlayerIds);
    }

    [Fact]
    public async Task ContinuingAfterAMission_StartsTheNextOneWithTheNextLeader()
    {
        var ct = CancellationToken.None;
        var mission = await StartMissionAsync(5, spiesOnTeam: 1);
        var leaderId = (await _service.GetAsync(_id, Host, ct)).Value.LeaderPlayerId;
        foreach (var caller in mission.Team)
        {
            await _service.PlayMissionCardAsync(_id, caller.Equals(mission.Spies[0]) ? false : true, caller, ct);
        }

        var continued = (await _service.ContinueAsync(_id, Host, ct)).Value;

        var order = continued.Players.Select(x => x.Id).ToList();
        Assert.Equal(order[(order.IndexOf(leaderId!.Value) + 1) % 5], continued.LeaderPlayerId);
        Assert.Equal(ResistancePhaseDto.SelectingTeam, continued.Phase);
        Assert.Equal(2, continued.MissionNumber);
        Assert.Equal(3, continued.RequiredTeamSize);
        Assert.Empty(continued.TeamPlayerIds);
        Assert.Empty(continued.PlayedMissionPlayerIds);
        Assert.Null(continued.Votes);
        Assert.Single(continued.MissionResults);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ThirdSuccessOrFailure_EndsTheGameAndRevealsTheRoles(bool resistanceWins)
    {
        var ct = CancellationToken.None;
        var callers = await OpenGameAndJoinAsync(5);
        await _service.StartAsync(_id, Host, ct);

        for (var round = 0; round < 3; round++)
        {
            await PlayRoundAsync(callers, fail: !resistanceWins);
            if (round < 2)
            {
                await _service.ContinueAsync(_id, Host, ct);
            }
        }

        var game = (await _service.GetAsync(_id, callers[0], ct)).Value;
        Assert.Equal(ResistancePhaseDto.GameOver, game.Phase);
        Assert.Equal(resistanceWins ? ResistanceRoleDto.Resistance : ResistanceRoleDto.Spy, game.Winner);
        Assert.Equal(3, game.MissionResults.Count(x => x.Succeeded == resistanceWins));
        Assert.Equal(5, game.RevealedRoles!.Count);
        Assert.Equal("Resistance.GameOver", (await _service.ContinueAsync(_id, Host, ct)).FirstError.Code);
        Assert.Equal("Resistance.GameOver", (await _service.PlayMissionCardAsync(_id, true, callers[0], ct)).FirstError.Code);
    }

    [Fact]
    public async Task TheGame_GoesOnAfterTwoSuccessesAndOneFailure()
    {
        var ct = CancellationToken.None;
        var callers = await OpenGameAndJoinAsync(5);
        await _service.StartAsync(_id, Host, ct);

        foreach (var fail in new[] { false, true, false })
        {
            await PlayRoundAsync(callers, fail);
            await _service.ContinueAsync(_id, Host, ct);
        }

        var game = (await _service.GetAsync(_id, Host, ct)).Value;
        Assert.Equal(ResistancePhaseDto.SelectingTeam, game.Phase);
        Assert.Equal(4, game.MissionNumber);
        Assert.Equal([true, false, true], game.MissionResults.Select(x => x.Succeeded));
        Assert.Null(game.Winner);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public async Task ARejectedTeam_AndATie_PassTheLeadershipAtOnceAndKeepTheVotes(int playerCount)
    {
        var ct = CancellationToken.None;
        var (callers, leader) = await StartWithTeamAsync(playerCount);
        var approvals = Enumerable.Range(0, playerCount).Select(i => i < playerCount / 2).ToArray();
        await VoteAsync(callers, approvals);

        var rejected = (await _service.GetAsync(_id, Host, ct)).Value;
        Assert.False(rejected.VoteApproved);
        Assert.Equal(1, rejected.RejectedTeams);
        Assert.Equal(ResistancePhaseDto.SelectingTeam, rejected.Phase);
        Assert.Empty(rejected.TeamPlayerIds);
        Assert.Equal(playerCount, rejected.Votes!.Count);

        var continued = rejected;

        var order = continued.Players.Select(x => x.Id).ToList();
        Assert.Equal(order[(order.IndexOf(leader.PlayerId!.Value) + 1) % playerCount], continued.LeaderPlayerId);
        Assert.Equal(ResistancePhaseDto.SelectingTeam, continued.Phase);
        Assert.Empty(continued.TeamPlayerIds);
        Assert.Equal(1, continued.RejectedTeams);
        Assert.Equal(1, continued.MissionNumber);
    }

    [Fact]
    public async Task TheLeadership_WrapsAroundTheSeatingOrder()
    {
        var ct = CancellationToken.None;
        var callers = await OpenGameAndJoinAsync(5);
        var started = (await _service.StartAsync(_id, Host, ct)).Value;
        var order = started.Players.Select(x => x.Id).ToList();
        var seen = new List<Guid?> { started.LeaderPlayerId };

        for (var i = 0; i < 4; i++)
        {
            var leader = callers.First(x => x.PlayerId == seen[^1]);
            await _service.SelectTeamAsync(_id, order.Take(2).ToList(), leader, ct);
            await VoteAsync(callers, false, false, false, false, false);
            seen.Add((await _service.GetAsync(_id, Host, ct)).Value.LeaderPlayerId);
        }

        Assert.Equal(5, seen.Distinct().Count());
        var start = order.IndexOf(seen[0]!.Value);
        for (var i = 0; i < seen.Count; i++)
        {
            Assert.Equal(order[(start + i) % 5], seen[i]);
        }
    }

    [Fact]
    public async Task Continuing_IsForTheLeaderAndManagersOnly_AndOnlyAfterAMission()
    {
        var ct = CancellationToken.None;
        var mission = await StartMissionAsync(5, spiesOnTeam: 1);
        foreach (var caller in mission.Team)
        {
            await _service.PlayMissionCardAsync(_id, !mission.Spies.Contains(caller), caller, ct);
        }

        var game = (await _service.GetAsync(_id, Host, ct)).Value;
        var other = mission.Team.First(x => x.PlayerId != game.LeaderPlayerId);
        var denied = await _service.ContinueAsync(_id, other, ct);
        Assert.Equal("Resistance.NotAllowed", denied.FirstError.Code);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, denied.FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.ContinueAsync(_id, new GameCaller("guest", false), ct)).FirstError.Type);
        Assert.False((await _service.ContinueAsync(_id, Host, ct)).IsError);
        Assert.Equal("Resistance.NothingToContinue", (await _service.ContinueAsync(_id, Host, ct)).FirstError.Code);
    }

    [Fact]
    public async Task FiveRejectedTeamsInARow_GiveTheSpiesTheWinAndEndTheRound()
    {
        var ct = CancellationToken.None;
        var callers = await OpenGameAndJoinAsync(5);
        var started = (await _service.StartAsync(_id, Host, ct)).Value;
        var team = started.Players.Take(2).Select(x => x.Id).ToList();
        var leaderId = started.LeaderPlayerId;

        for (var i = 1; i <= 5; i++)
        {
            await _service.SelectTeamAsync(_id, team, callers.First(x => x.PlayerId == leaderId), ct);
            await VoteAsync(callers, false, false, false, false, false);
            if (i < 5)
            {
                Assert.Equal(i, (await _service.GetAsync(_id, Host, ct)).Value.RejectedTeams);
                leaderId = (await _service.GetAsync(_id, Host, ct)).Value.LeaderPlayerId;
            }
        }

        var over = (await _service.GetAsync(_id, callers[0], ct)).Value;
        Assert.Equal(ResistancePhaseDto.GameOver, over.Phase);
        Assert.Equal(ResistanceRoleDto.Spy, over.Winner);
        Assert.Equal(5, over.RejectedTeams);
        Assert.Equal(5, over.Votes!.Count);
        Assert.Equal(5, over.RevealedRoles!.Count);
        Assert.Equal("Resistance.GameOver", (await _service.ContinueAsync(_id, Host, ct)).FirstError.Code);
        Assert.Equal("Resistance.GameOver", (await _service.VoteAsync(_id, true, callers[0], ct)).FirstError.Code);
        Assert.Equal("Resistance.GameOver", (await _service.SelectTeamAsync(_id, team, callers.First(x => x.PlayerId == leaderId), ct)).FirstError.Code);
    }

    private async Task<List<GameCaller>> EndGameAsync()
    {
        var ct = CancellationToken.None;
        var callers = await OpenGameAndJoinAsync(5);
        var started = (await _service.StartAsync(_id, Host, ct)).Value;
        var team = started.Players.Take(2).Select(x => x.Id).ToList();
        var leaderId = started.LeaderPlayerId;
        for (var i = 1; i <= 5; i++)
        {
            await _service.SelectTeamAsync(_id, team, callers.First(x => x.PlayerId == leaderId), ct);
            await VoteAsync(callers, false, false, false, false, false);
            leaderId = (await _service.GetAsync(_id, Host, ct)).Value.LeaderPlayerId;
        }

        return callers;
    }

    [Fact]
    public async Task Restart_AfterTheGameEnded_ReturnsToTheOpenLobby()
    {
        var ct = CancellationToken.None;
        var callers = await EndGameAsync();

        var before = _notifier.ResistanceChanges.Count;
        var restarted = await _service.RestartAsync(_id, Host, ct);

        Assert.False(restarted.IsError);
        var game = restarted.Value;
        Assert.Equal(1, game.MissionNumber);
        Assert.Equal(0, game.RejectedTeams);
        Assert.Null(game.Winner);
        Assert.Null(game.RevealedRoles);
        Assert.Null(game.Votes);
        Assert.Empty(game.MissionResults);
        Assert.Equal(5, game.Players.Count);
        Assert.Null(game.StartedAt);
        Assert.True(game.IsOpen);
        Assert.Null(game.LeaderPlayerId);
        Assert.All(callers, x => Assert.Null(_service.GetAsync(_id, x, ct).Result.Value.YourRole));
        Assert.Equal(before + 1, _notifier.ResistanceChanges.Count);
    }

    [Fact]
    public async Task Restart_WhileTheGameIsOn_Conflicts()
    {
        await StartWithTeamAsync(5);

        var result = await _service.RestartAsync(_id, Host, CancellationToken.None);

        Assert.Equal("Resistance.NotOver", result.FirstError.Code);
    }

    [Fact]
    public async Task Restart_BeforeTheStart_Conflicts()
    {
        await OpenGameAndJoinAsync(5);

        var result = await _service.RestartAsync(_id, Host, CancellationToken.None);

        Assert.Equal("Resistance.NotStarted", result.FirstError.Code);
    }

    [Fact]
    public async Task Restart_IsNotForPlayers()
    {
        var callers = await EndGameAsync();

        var result = await _service.RestartAsync(_id, callers[0], CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
    }

    [Fact]
    public async Task AnApproval_ResetsTheRejectionCount()
    {
        var ct = CancellationToken.None;
        var (callers, leader) = await StartWithTeamAsync(5);
        await VoteAsync(callers, false, false, false, false, false);
        var next = (await _service.GetAsync(_id, Host, ct)).Value;
        var team = next.Players.Take(2).Select(x => x.Id).ToList();
        await _service.SelectTeamAsync(_id, team, callers.First(x => x.PlayerId == next.LeaderPlayerId), ct);

        await VoteAsync(callers, true, true, true, true, true);

        Assert.NotEqual(leader.PlayerId, next.LeaderPlayerId);
        Assert.Equal(0, (await _service.GetAsync(_id, Host, ct)).Value.RejectedTeams);
    }

    [Fact]
    public async Task Roles_StayHiddenWhileTheGameIsOn()
    {
        var (callers, _) = await StartWithTeamAsync(5);
        await VoteAsync(callers, false, false, false, false, false);

        var game = (await _service.GetAsync(_id, callers[0], CancellationToken.None)).Value;

        Assert.Null(game.Winner);
        Assert.Null(game.RevealedRoles);
    }

    [Theory]
    [InlineData(5, 2)]
    [InlineData(6, 2)]
    [InlineData(7, 3)]
    [InlineData(8, 3)]
    [InlineData(9, 3)]
    [InlineData(10, 4)]
    public async Task Starting_DealsAThirdOfThePlayersAsSpiesRoundedUp(int players, int spies)
    {
        await OpenGameAsync();
        var callers = await JoinPlayersAsync(players);
        await _service.StartAsync(_id, Host, CancellationToken.None);

        var game = await _games.GetAsync(_id, CancellationToken.None);

        Assert.Equal(spies, game!.Players.Count(x => x.Role == ResistanceRole.Spy));
        Assert.Equal(players - spies, game.Players.Count(x => x.Role == ResistanceRole.Resistance));
        Assert.Equal(callers.Count, game.Players.Count);
    }

    [Fact]
    public async Task AfterStarting_PlayersCannotJoinBeKickedOrReordered()
    {
        await OpenGameAsync();
        var callers = await JoinPlayersAsync(5);
        await _service.StartAsync(_id, Host, CancellationToken.None);
        var ids = callers.Select(x => x.PlayerId!.Value).ToList();

        var join = await _service.JoinAsync(_id, "Late", new GameCaller("late", false), CancellationToken.None);
        var kick = await _service.KickAsync(_id, ids[0], Admin, CancellationToken.None);
        var reorder = await _service.ReorderAsync(_id, ids, Admin, CancellationToken.None);

        Assert.Equal("Resistance.Started", join.FirstError.Code);
        Assert.Equal("Resistance.Started", kick.FirstError.Code);
        Assert.Equal("Resistance.Started", reorder.FirstError.Code);
    }

    [Fact]
    public async Task AfterStarting_EachPlayerSeesOnlyTheirOwnRoleAndSpiesSeeEachOther()
    {
        await OpenGameAsync();
        var callers = await JoinPlayersAsync(7);
        await _service.StartAsync(_id, Host, CancellationToken.None);
        var game = await _games.GetAsync(_id, CancellationToken.None);

        foreach (var caller in callers)
        {
            var dto = (await _service.GetAsync(_id, caller, CancellationToken.None)).Value;
            var role = game!.Players.Single(x => x.PlayerId == caller.PlayerId).Role;

            Assert.Equal((ResistanceRoleDto?)role, dto.YourRole);
            var otherSpies = game.Players
                .Where(x => x.Role == ResistanceRole.Spy && x.PlayerId != caller.PlayerId)
                .Select(x => x.PlayerId);
            if (role == ResistanceRole.Spy)
            {
                Assert.Equal(otherSpies.Order(), dto.FellowSpies!.Order());
            }
            else
            {
                Assert.Null(dto.FellowSpies);
            }
        }
    }

    [Fact]
    public async Task AfterStarting_ObserversSeeNoRolesButSeeTheLeader()
    {
        await OpenGameAsync();
        await JoinPlayersAsync(5);
        await _service.StartAsync(_id, Host, CancellationToken.None);

        foreach (var observer in new[] { Admin, Host, new GameCaller("guest", false) })
        {
            var dto = (await _service.GetAsync(_id, observer, CancellationToken.None)).Value;

            Assert.Null(dto.YourRole);
            Assert.Null(dto.FellowSpies);
            Assert.NotNull(dto.LeaderPlayerId);
        }
    }

    [Fact]
    public async Task TheLobby_ShowsWhetherTheGameStarted()
    {
        await OpenGameAsync();
        await JoinPlayersAsync(5);
        Assert.False((await _service.GetLobbyAsync(_id, Host, CancellationToken.None)).Value.IsStarted);

        await _service.StartAsync(_id, Host, CancellationToken.None);

        Assert.True((await _service.GetLobbyAsync(_id, Host, CancellationToken.None)).Value.IsStarted);
    }

    [Theory]
    [InlineData(ResistanceRoleDto.Spy, "\"Spy\"")]
    [InlineData(ResistanceRoleDto.Resistance, "\"Resistance\"")]
    public void TheRole_IsSerializedByNameSoTheUiCanCompareIt(ResistanceRoleDto role, string expected)
    {
        Assert.Equal(expected, global::System.Text.Json.JsonSerializer.Serialize(role));
    }

    private async Task<(List<GameCaller> Callers, GameCaller Leader)> StartWithTeamAsync(int count)
    {
        var ct = CancellationToken.None;
        var callers = await OpenGameAndJoinAsync(count);
        var started = (await _service.StartAsync(_id, Host, ct)).Value;
        var leader = callers.First(x => x.PlayerId == started.LeaderPlayerId);
        await _service.SelectTeamAsync(_id, started.Players.Take(2).Select(x => x.Id).ToList(), leader, ct);

        return (callers, leader);
    }

    /// <summary>
    /// Starts a game whose leader submits a team of <paramref name="spiesOnTeam"/> spies filled up with members of the
    /// resistance, and everybody approves it. Spies and the resistance are told apart by the role each one sees.
    /// </summary>
    private async Task<(List<GameCaller> Team, List<GameCaller> Spies, List<GameCaller> Resistance)> StartMissionAsync(
        int count, int spiesOnTeam)
    {
        var ct = CancellationToken.None;
        var callers = await OpenGameAndJoinAsync(count);
        var started = (await _service.StartAsync(_id, Host, ct)).Value;

        var spies = new List<GameCaller>();
        var resistance = new List<GameCaller>();
        foreach (var caller in callers)
        {
            var role = (await _service.GetAsync(_id, caller, ct)).Value.YourRole;
            (role == ResistanceRoleDto.Spy ? spies : resistance).Add(caller);
        }

        var team = spies.Take(spiesOnTeam).Concat(resistance).Take(started.RequiredTeamSize!.Value).ToList();
        var leader = callers.First(x => x.PlayerId == started.LeaderPlayerId);
        var selected = await _service.SelectTeamAsync(_id, team.Select(x => x.PlayerId!.Value).ToList(), leader, ct);
        Assert.False(selected.IsError);
        await VoteAsync(callers, Enumerable.Repeat(true, count).ToArray());

        return (
            team,
            spies.Where(team.Contains).ToList(),
            resistance.Where(team.Contains).ToList());
    }

    /// <summary>
    /// Plays one whole mission: the leader picks a team (with a spy to play Fail when <paramref name="fail"/>, else
    /// only members of the resistance), everybody approves and the team plays.
    /// </summary>
    private async Task PlayRoundAsync(List<GameCaller> callers, bool fail)
    {
        var ct = CancellationToken.None;
        var game = (await _service.GetAsync(_id, Host, ct)).Value;
        var roles = new Dictionary<Guid, ResistanceRoleDto>();
        foreach (var caller in callers)
        {
            roles[caller.PlayerId!.Value] = (await _service.GetAsync(_id, caller, ct)).Value.YourRole!.Value;
        }

        var spies = callers.Where(x => roles[x.PlayerId!.Value] == ResistanceRoleDto.Spy);
        var resistance = callers.Where(x => roles[x.PlayerId!.Value] == ResistanceRoleDto.Resistance);
        var team = (fail ? spies.Take(1).Concat(resistance) : resistance).Take(game.RequiredTeamSize!.Value).ToList();

        var leader = callers.First(x => x.PlayerId == game.LeaderPlayerId);
        Assert.False((await _service.SelectTeamAsync(_id, team.Select(x => x.PlayerId!.Value).ToList(), leader, ct)).IsError);
        await VoteAsync(callers, Enumerable.Repeat(true, callers.Count).ToArray());
        foreach (var caller in team)
        {
            var success = roles[caller.PlayerId!.Value] == ResistanceRoleDto.Resistance;
            Assert.False((await _service.PlayMissionCardAsync(_id, success, caller, ct)).IsError);
        }
    }

    /// <summary>Every player votes, in joining order.</summary>
    private async Task VoteAsync(List<GameCaller> callers, params bool[] approvals)
    {
        for (var i = 0; i < callers.Count; i++)
        {
            var result = await _service.VoteAsync(_id, approvals[i], callers[i], CancellationToken.None);
            Assert.False(result.IsError);
        }
    }

    private async Task<List<GameCaller>> JoinPlayersAsync(int count)
    {
        var callers = new List<GameCaller>();
        for (var i = 1; i <= count; i++)
        {
            callers.Add(await JoinAsync($"Player {i}"));
        }

        return callers;
    }

    private async Task<List<GameCaller>> OpenGameAndJoinAsync(int count)
    {
        await OpenGameAsync();

        return await JoinPlayersAsync(count);
    }

    private async Task OpenGameAsync()
    {
        await _service.CreateAsync(_id, Host, CancellationToken.None);
        await _service.OpenAsync(_id, Admin, CancellationToken.None);
    }

    private async Task<GameCaller> JoinAsync(string name)
    {
        var joined = await _service.JoinAsync(_id, name, new GameCaller("guest", false), CancellationToken.None);

        return new GameCaller("guest", false, joined.Value);
    }

    private sealed class FakeRulesProvider : IResistanceRulesProvider
    {
        public ResistanceRules Rules { get; set; } = ResistanceRules.Default();

        public ResistanceRules Current() => new()
        {
            PlayerCounts = new Dictionary<int, ResistanceCountRules>(Rules.PlayerCounts),
        };
    }

    private sealed class FakeResistanceGamesRepository : IResistanceGamesRepository
    {
        private readonly Dictionary<Guid, ResistanceGame> _games = [];

        public Task<ResistanceGame?> GetAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(_games.GetValueOrDefault(sessionId));

        public Task<bool> TryCreateAsync(ResistanceGame game, CancellationToken cancellationToken) =>
            Task.FromResult(_games.TryAdd(game.SessionId, game));

        public Task<ResistanceRulesResult> RefreshRulesAsync(
            Guid sessionId, ResistanceRules rules, CancellationToken cancellationToken)
        {
            var game = _games.GetValueOrDefault(sessionId);
            if (game is null)
            {
                return Task.FromResult(new ResistanceRulesResult(ResistanceRulesStatus.NoGame, null));
            }

            if (game.IsOpen || game.IsStarted)
            {
                return Task.FromResult(new ResistanceRulesResult(ResistanceRulesStatus.NotEditable, game));
            }

            rules.PlotCardsEnabled = game.Rules.PlotCardsEnabled;
            game.Rules = rules;
            return Task.FromResult(new ResistanceRulesResult(ResistanceRulesStatus.Refreshed, game));
        }

        public Task<ResistanceRulesResult> SetPlotCardsAsync(
            Guid sessionId, bool enabled, CancellationToken cancellationToken)
        {
            var game = _games.GetValueOrDefault(sessionId);
            if (game is null)
            {
                return Task.FromResult(new ResistanceRulesResult(ResistanceRulesStatus.NoGame, null));
            }

            if (game.IsStarted)
            {
                return Task.FromResult(new ResistanceRulesResult(ResistanceRulesStatus.NotEditable, game));
            }

            game.Rules.PlotCardsEnabled = enabled;
            return Task.FromResult(new ResistanceRulesResult(ResistanceRulesStatus.Refreshed, game));
        }

        public Task<ResistanceGame?> OpenAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            var game = _games.GetValueOrDefault(sessionId);
            if (game is not null)
            {
                game.IsOpen = true;
            }

            return Task.FromResult(game);
        }

        public Task<LobbyResult<ResistanceGame>> JoinAsync(
            Guid sessionId, ResistancePlayer player, CancellationToken cancellationToken)
        {
            if (!_games.TryGetValue(sessionId, out var game))
            {
                return Result(LobbyStatus.NoGame, null);
            }

            if (!game.IsOpen)
            {
                return Result(LobbyStatus.NotOpen, game);
            }

            if (game.IsStarted)
            {
                return Result(LobbyStatus.Started, game);
            }

            if (game.Players.Any(x => x.NormalizedName == player.NormalizedName))
            {
                return Result(LobbyStatus.NameTaken, game);
            }

            if (game.Players.Count >= ResistanceGame.MaxPlayers)
            {
                return Result(LobbyStatus.Full, game);
            }

            player.SessionId = sessionId;
            player.Order = game.Players.Count;
            game.Players.Add(player);

            return Result(LobbyStatus.Done, game);
        }

        public Task<LobbyResult<ResistanceGame>> KickAsync(
            Guid sessionId, Guid playerId, CancellationToken cancellationToken)
        {
            if (!_games.TryGetValue(sessionId, out var game))
            {
                return Result(LobbyStatus.NoGame, null);
            }

            if (game.IsStarted)
            {
                return Result(LobbyStatus.Started, game);
            }

            var kicked = game.Players.FirstOrDefault(x => x.PlayerId == playerId);
            if (kicked is null)
            {
                return Result(LobbyStatus.NoPlayer, game);
            }

            game.Players.Remove(kicked);
            var order = 0;
            foreach (var player in game.Players.OrderBy(x => x.Order))
            {
                player.Order = order++;
            }

            return Result(LobbyStatus.Done, game);
        }

        public Task<LobbyResult<ResistanceGame>> ReorderAsync(
            Guid sessionId, IReadOnlyList<Guid> playerIds, CancellationToken cancellationToken)
        {
            if (!_games.TryGetValue(sessionId, out var game))
            {
                return Result(LobbyStatus.NoGame, null);
            }

            if (game.IsStarted)
            {
                return Result(LobbyStatus.Started, game);
            }

            if (playerIds.Count != game.Players.Count || game.Players.Any(x => !playerIds.Contains(x.PlayerId)))
            {
                return Result(LobbyStatus.OrderMismatch, game);
            }

            foreach (var player in game.Players)
            {
                player.Order = playerIds.ToList().IndexOf(player.PlayerId);
            }

            return Result(LobbyStatus.Done, game);
        }

        public Task<bool> DeleteAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(_games.Remove(sessionId));

        public Task<ResistanceStartResult> StartAsync(
            Guid sessionId, DateTimeOffset startedAt, CancellationToken cancellationToken)
        {
            var status = ResistanceStartStatus.Started;
            _games.TryGetValue(sessionId, out var game);
            if (game is null)
            {
                status = ResistanceStartStatus.NoGame;
            }
            else if (game.IsStarted)
            {
                status = ResistanceStartStatus.AlreadyStarted;
            }
            else if (!game.IsOpen)
            {
                status = ResistanceStartStatus.NotOpen;
            }
            else if (game.Players.Count < ResistanceGame.MinPlayers)
            {
                status = ResistanceStartStatus.NotEnoughPlayers;
            }
            else
            {
                ResistanceRoles.Deal(game, new Random(1));
                game.StartedAt = startedAt;
            }

            return Task.FromResult(new ResistanceStartResult(status, game));
        }

        public Task<ResistanceRestartResult> RestartAsync(
            Guid sessionId, CancellationToken cancellationToken)
        {
            var status = ResistanceRestartStatus.Restarted;
            _games.TryGetValue(sessionId, out var game);
            if (game is null)
            {
                status = ResistanceRestartStatus.NoGame;
            }
            else if (!game.IsStarted)
            {
                status = ResistanceRestartStatus.NotStarted;
            }
            else if (game.Phase != ResistancePhase.GameOver)
            {
                status = ResistanceRestartStatus.NotOver;
            }
            else
            {
                ResistanceRoles.Reset(game);
                game.StartedAt = null;
            }

            return Task.FromResult(new ResistanceRestartResult(status, game));
        }

        public Task<ResistanceTeamResult> SelectTeamAsync(
            Guid sessionId, Guid? callerPlayerId, IReadOnlyList<Guid> playerIds, CancellationToken cancellationToken)
        {
            _games.TryGetValue(sessionId, out var game);
            var status = ResistanceTeamStatus.Selected;
            if (game is null)
            {
                status = ResistanceTeamStatus.NoGame;
            }
            else if (!game.IsStarted)
            {
                status = ResistanceTeamStatus.NotStarted;
            }
            else if (game.Phase == ResistancePhase.GameOver)
            {
                status = ResistanceTeamStatus.GameOver;
            }
            else if (callerPlayerId is null || callerPlayerId != game.LeaderPlayerId)
            {
                status = ResistanceTeamStatus.NotLeader;
            }
            else if (game.Phase == ResistancePhase.DealingCards)
            {
                status = ResistanceTeamStatus.CardsNotDealt;
            }
            else if (game.Phase != ResistancePhase.SelectingTeam)
            {
                status = ResistanceTeamStatus.AlreadySubmitted;
            }
            else if (playerIds.Count != game.Rules.TeamSize(game.Players.Count, game.MissionNumber)
                || playerIds.Distinct().Count() != playerIds.Count)
            {
                status = ResistanceTeamStatus.WrongSize;
            }
            else if (playerIds.Any(id => game.Players.All(x => x.PlayerId != id)))
            {
                status = ResistanceTeamStatus.UnknownPlayer;
            }
            else
            {
                foreach (var player in game.Players)
                {
                    player.IsOnTeam = playerIds.Contains(player.PlayerId);
                    player.Vote = null;
                    player.MissionCard = null;
                }

                game.Phase = ResistancePhase.Voting;
            }

            return Task.FromResult(new ResistanceTeamResult(status, game));
        }

        /// <summary>What playing or confirming a plot card answers; the rules behind it are the repository's, tested there.</summary>
        public ResistancePlayCardStatus PlayCardStatus { get; set; } = ResistancePlayCardStatus.Done;

        public Task<ResistancePlayCardResult> PlayPlotCardAsync(
            Guid sessionId, Guid? callerPlayerId, int cardId, Guid targetPlayerId, CancellationToken cancellationToken) =>
            Task.FromResult(new ResistancePlayCardResult(PlayCardStatus, _games.GetValueOrDefault(sessionId)));

        public Task<ResistancePlayCardResult> TakeResponsibilityAsync(
            Guid sessionId, Guid? callerPlayerId, int cardId, Guid targetPlayerId, bool? takeOpinionMaker,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ResistancePlayCardResult(PlayCardStatus, _games.GetValueOrDefault(sessionId)));

        public Task<ResistancePlayCardResult> TakeLeadAsync(
            Guid sessionId, Guid? callerPlayerId, int cardId, CancellationToken cancellationToken) =>
            Task.FromResult(new ResistancePlayCardResult(PlayCardStatus, _games.GetValueOrDefault(sessionId)));

        public Task<ResistancePlayCardResult> PeekMissionCardAsync(
            Guid sessionId, Guid? callerPlayerId, int cardId, Guid targetPlayerId, CancellationToken cancellationToken) =>
            Task.FromResult(new ResistancePlayCardResult(PlayCardStatus, _games.GetValueOrDefault(sessionId)));

        public Task<ResistancePlayCardResult> PlayNoConfidenceAsync(
            Guid sessionId, Guid? callerPlayerId, int cardId, CancellationToken cancellationToken) =>
            Task.FromResult(new ResistancePlayCardResult(PlayCardStatus, _games.GetValueOrDefault(sessionId)));

        public Task<ResistancePlayCardResult> PassLeaderWindowAsync(
            Guid sessionId, Guid? callerPlayerId, CancellationToken cancellationToken) =>
            Task.FromResult(new ResistancePlayCardResult(PlayCardStatus, _games.GetValueOrDefault(sessionId)));

        public Task<ResistancePlayCardResult> ConfirmPlotCardAsync(
            Guid sessionId, Guid? callerPlayerId, int cardId, CancellationToken cancellationToken) =>
            Task.FromResult(new ResistancePlayCardResult(PlayCardStatus, _games.GetValueOrDefault(sessionId)));

        public Task<ResistanceDealResult> DealPlotCardsAsync(
            Guid sessionId, Guid? callerPlayerId, IReadOnlyList<PlotCardAssignment> assignments,
            CancellationToken cancellationToken)
        {
            _games.TryGetValue(sessionId, out var game);
            var status = ResistanceDealStatus.Dealt;
            var toDeal = game is null ? [] : ResistanceRoles.CardsToDeal(game).ToDictionary(x => x.CardId);
            if (game is null)
            {
                status = ResistanceDealStatus.NoGame;
            }
            else if (!game.IsStarted)
            {
                status = ResistanceDealStatus.NotStarted;
            }
            else if (game.Phase == ResistancePhase.GameOver)
            {
                status = ResistanceDealStatus.GameOver;
            }
            else if (callerPlayerId is null || callerPlayerId != game.LeaderPlayerId)
            {
                status = ResistanceDealStatus.NotLeader;
            }
            else if (game.Phase != ResistancePhase.DealingCards)
            {
                status = ResistanceDealStatus.NotDealing;
            }
            else if (assignments.Any(x => !toDeal.ContainsKey(x.CardId))
                     || assignments.Select(x => x.CardId).Distinct().Count() != assignments.Count)
            {
                status = ResistanceDealStatus.UnknownCard;
            }
            else if (assignments.Any(x => x.PlayerId == game.LeaderPlayerId || game.Players.All(p => p.PlayerId != x.PlayerId)))
            {
                status = ResistanceDealStatus.InvalidReceiver;
            }
            else if (assignments.Count != toDeal.Count)
            {
                status = ResistanceDealStatus.Incomplete;
            }
            else
            {
                foreach (var assignment in assignments)
                {
                    toDeal[assignment.CardId].Location = ResistancePlotCardLocation.Hand;
                    toDeal[assignment.CardId].HolderPlayerId = assignment.PlayerId;
                }

                foreach (var kept in game!.PlotCards.Where(x => x.Location == ResistancePlotCardLocation.Drawn))
                {
                    kept.Location = ResistancePlotCardLocation.Hand;
                }

                game.Phase = ResistancePhase.SelectingTeam;
            }

            return Task.FromResult(new ResistanceDealResult(status, game));
        }

        public Task<ResistanceVoteResult> VoteAsync(
            Guid sessionId, Guid? callerPlayerId, bool approve, CancellationToken cancellationToken)
        {
            _games.TryGetValue(sessionId, out var game);
            var voter = game?.Players.FirstOrDefault(x => x.PlayerId == callerPlayerId);
            var status = ResistanceVoteStatus.Voted;
            if (game is null)
            {
                status = ResistanceVoteStatus.NoGame;
            }
            else if (!game.IsStarted)
            {
                status = ResistanceVoteStatus.NotStarted;
            }
            else if (game.Phase == ResistancePhase.GameOver)
            {
                status = ResistanceVoteStatus.GameOver;
            }
            else if (voter is null)
            {
                status = ResistanceVoteStatus.NotPlayer;
            }
            else if (game.Phase != ResistancePhase.Voting)
            {
                status = ResistanceVoteStatus.NotVoting;
            }
            else
            {
                voter.Vote = approve;
                if (game.Players.All(x => x.Vote is not null))
                {
                    ResistanceRoles.ResolveVotes(game);
                }
            }

            return Task.FromResult(new ResistanceVoteResult(status, game));
        }

        public Task<ResistanceMissionCardResult> PlayMissionCardAsync(
            Guid sessionId, Guid? callerPlayerId, bool success, CancellationToken cancellationToken)
        {
            _games.TryGetValue(sessionId, out var game);
            var player = game?.Players.FirstOrDefault(x => x.PlayerId == callerPlayerId);
            var status = ResistanceMissionStatus.Played;
            if (game is null)
            {
                status = ResistanceMissionStatus.NoGame;
            }
            else if (!game.IsStarted)
            {
                status = ResistanceMissionStatus.NotStarted;
            }
            else if (game.Phase == ResistancePhase.GameOver)
            {
                status = ResistanceMissionStatus.GameOver;
            }
            else if (player is null)
            {
                status = ResistanceMissionStatus.NotPlayer;
            }
            else if (game.Phase != ResistancePhase.PlayingMission)
            {
                status = ResistanceMissionStatus.NotPlaying;
            }
            else if (!player.IsOnTeam)
            {
                status = ResistanceMissionStatus.NotOnTeam;
            }
            else if (player.MissionCard is not null)
            {
                status = ResistanceMissionStatus.AlreadyPlayed;
            }
            else if (!success && player.Role == ResistanceRole.Resistance)
            {
                status = ResistanceMissionStatus.MustSucceed;
            }
            else
            {
                player.MissionCard = success;
                if (ResistanceRoles.AllCardsPlayed(game))
                {
                    ResistanceRoles.ResolveMission(game);
                }
            }

            return Task.FromResult(new ResistanceMissionCardResult(status, game));
        }

        public Task<ResistanceContinueResult> ContinueAsync(
            Guid sessionId, Guid? callerPlayerId, bool isManager, CancellationToken cancellationToken)
        {
            _games.TryGetValue(sessionId, out var game);
            var status = ResistanceContinueStatus.Continued;
            if (game is null)
            {
                status = ResistanceContinueStatus.NoGame;
            }
            else if (!game.IsStarted)
            {
                status = ResistanceContinueStatus.NotStarted;
            }
            else if (game.Phase == ResistancePhase.GameOver)
            {
                status = ResistanceContinueStatus.GameOver;
            }
            else if (!isManager && (callerPlayerId is null || callerPlayerId != game.LeaderPlayerId))
            {
                status = ResistanceContinueStatus.NotAllowed;
            }
            else if (game.Phase != ResistancePhase.MissionResult)
            {
                status = ResistanceContinueStatus.NothingToContinue;
            }
            else
            {
                ResistanceRoles.PassLeadership(game);
            }

            return Task.FromResult(new ResistanceContinueResult(status, game));
        }

        private static Task<LobbyResult<ResistanceGame>> Result(LobbyStatus status, ResistanceGame? game) =>
            Task.FromResult(new LobbyResult<ResistanceGame>(status, game));
    }
}
