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
            Assert.Equal(ResistancePhaseDto.VoteResult, game.Phase);
            Assert.True(game.VoteApproved);
            Assert.Equal(5, game.Votes!.Count);
            Assert.Equal(3, game.Votes.Count(x => x.Approve));
            Assert.Equal(0, game.RejectedTeams);
        }
    }

    [Fact]
    public async Task AnApprovedTeam_StaysPutAndCannotBeContinuedFrom()
    {
        var ct = CancellationToken.None;
        var (callers, leader) = await StartWithTeamAsync(5);
        await VoteAsync(callers, true, true, true, true, true);

        var result = await _service.ContinueAsync(_id, leader, ct);

        Assert.Equal("Resistance.NothingToContinue", result.FirstError.Code);
        var game = (await _service.GetAsync(_id, Host, ct)).Value;
        Assert.Equal(2, game.TeamPlayerIds.Count);
        Assert.Equal(ResistancePhaseDto.VoteResult, game.Phase);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public async Task ARejectedTeam_AndATie_PassTheLeadershipOnContinue(int playerCount)
    {
        var ct = CancellationToken.None;
        var (callers, leader) = await StartWithTeamAsync(playerCount);
        var approvals = Enumerable.Range(0, playerCount).Select(i => i < playerCount / 2).ToArray();
        await VoteAsync(callers, approvals);

        var rejected = (await _service.GetAsync(_id, Host, ct)).Value;
        Assert.False(rejected.VoteApproved);
        Assert.Equal(1, rejected.RejectedTeams);
        Assert.Equal(ResistancePhaseDto.VoteResult, rejected.Phase);
        Assert.Equal(2, rejected.TeamPlayerIds.Count);

        var continued = (await _service.ContinueAsync(_id, Host, ct)).Value;

        var order = continued.Players.Select(x => x.Id).ToList();
        Assert.Equal(order[(order.IndexOf(leader.PlayerId!.Value) + 1) % playerCount], continued.LeaderPlayerId);
        Assert.Equal(ResistancePhaseDto.SelectingTeam, continued.Phase);
        Assert.Empty(continued.TeamPlayerIds);
        Assert.Empty(continued.VotedPlayerIds);
        Assert.Null(continued.Votes);
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
            seen.Add((await _service.ContinueAsync(_id, Host, ct)).Value.LeaderPlayerId);
        }

        Assert.Equal(5, seen.Distinct().Count());
        var start = order.IndexOf(seen[0]!.Value);
        for (var i = 0; i < seen.Count; i++)
        {
            Assert.Equal(order[(start + i) % 5], seen[i]);
        }
    }

    [Fact]
    public async Task Continuing_IsForTheLeaderAndManagersOnly()
    {
        var ct = CancellationToken.None;
        var (callers, leader) = await StartWithTeamAsync(5);
        Assert.Equal("Resistance.NothingToContinue", (await _service.ContinueAsync(_id, leader, ct)).FirstError.Code);
        await VoteAsync(callers, false, false, false, false, false);

        var other = callers.First(x => x.PlayerId != leader.PlayerId);
        var denied = await _service.ContinueAsync(_id, other, ct);
        Assert.Equal("Resistance.NotAllowed", denied.FirstError.Code);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, denied.FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.ContinueAsync(_id, new GameCaller("guest", false), ct)).FirstError.Type);
        Assert.False((await _service.ContinueAsync(_id, leader, ct)).IsError);
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
                leaderId = (await _service.ContinueAsync(_id, Host, ct)).Value.LeaderPlayerId;
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

    [Fact]
    public async Task AnApproval_ResetsTheRejectionCount()
    {
        var ct = CancellationToken.None;
        var (callers, leader) = await StartWithTeamAsync(5);
        await VoteAsync(callers, false, false, false, false, false);
        var next = (await _service.ContinueAsync(_id, Host, ct)).Value;
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

            game.Rules = rules;
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
                }

                game.Phase = ResistancePhase.Voting;
            }

            return Task.FromResult(new ResistanceTeamResult(status, game));
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
            else if (game.Phase != ResistancePhase.VoteResult || ResistanceRoles.IsApproved(game))
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
