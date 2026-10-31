using ErrorOr;
using PartyKing.Application.Games.Services;
using PartyKing.Contract.V1.Games.Resistance;
using PartyKing.Domain.Entities.Games;

namespace PartyKing.Application.Tests;

public class ResistanceRoundServiceTests : ResistanceServiceTestBase
{
    [Fact]
    public async Task Starting_IsForManagersOfAnOpenGameWithFivePlayers()
    {
        var ct = CancellationToken.None;
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _roundService.StartAsync(_id, new GameCaller("guest", false), ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.NotFound, (await _roundService.StartAsync(_id, Host, ct)).FirstError.Type);

        await _lobbyService.CreateAsync(_id, Host, ct);
        Assert.Equal("Resistance.NotOpen", (await _roundService.StartAsync(_id, Host, ct)).FirstError.Code);

        await _lobbyService.OpenAsync(_id, Admin, ct);
        await JoinPlayersAsync(4);
        Assert.Equal("Resistance.NotEnoughPlayers", (await _roundService.StartAsync(_id, Host, ct)).FirstError.Code);

        await JoinAsync("Player 5");
        var started = await _roundService.StartAsync(_id, Admin, ct);

        Assert.NotNull(started.Value.StartedAt);
        Assert.Contains(started.Value.Players, x => x.Id == started.Value.LeaderPlayerId);
        Assert.Equal("Resistance.AlreadyStarted", (await _roundService.StartAsync(_id, Host, ct)).FirstError.Code);
    }

    [Fact]
    public async Task Starting_IsPushedOnce()
    {
        await OpenGameAsync();
        await JoinPlayersAsync(5);
        var before = _notifier.ResistanceChanges.Count;

        await _roundService.StartAsync(_id, Host, CancellationToken.None);

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
        var started = await _roundService.StartAsync(_id, Host, CancellationToken.None);

        Assert.Equal(1, started.Value.MissionNumber);
        Assert.Equal(3, started.Value.RequiredTeamSize);
        Assert.Empty(started.Value.TeamPlayerIds);
        Assert.Equal(8, callers.Count);
    }

    [Fact]
    public async Task SelectingATeam_IsOnlyForTheLeaderAndNeedsAStartedGame()
    {
        var ct = CancellationToken.None;
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _roundService.SelectTeamAsync(_id, [], new GameCaller("guest", false), ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.NotFound, (await _roundService.SelectTeamAsync(_id, [], Host, ct)).FirstError.Type);

        var callers = await OpenGameAndJoinAsync(5);
        Assert.Equal("Resistance.NotStarted", (await _roundService.SelectTeamAsync(_id, [], Host, ct)).FirstError.Code);

        var started = await _roundService.StartAsync(_id, Host, ct);
        var ids = started.Value.Players.Select(x => x.Id).ToList();
        var notLeader = callers.First(x => x.PlayerId != started.Value.LeaderPlayerId);

        var denied = await _roundService.SelectTeamAsync(_id, ids.Take(2).ToList(), notLeader, ct);
        var manager = await _roundService.SelectTeamAsync(_id, ids.Take(2).ToList(), Admin, ct);

        Assert.Equal("Resistance.NotLeader", denied.FirstError.Code);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, denied.FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, manager.FirstError.Type);
    }

    [Fact]
    public async Task SelectingATeam_NeedsExactlyTheRequiredNumberOfDistinctPlayers()
    {
        var ct = CancellationToken.None;
        var callers = await OpenGameAndJoinAsync(5);
        var started = await _roundService.StartAsync(_id, Host, ct);
        var leader = callers.First(x => x.PlayerId == started.Value.LeaderPlayerId);
        var ids = started.Value.Players.Select(x => x.Id).ToList();

        Assert.Equal("Resistance.WrongTeamSize", (await _roundService.SelectTeamAsync(_id, ids.Take(1).ToList(), leader, ct)).FirstError.Code);
        Assert.Equal("Resistance.WrongTeamSize", (await _roundService.SelectTeamAsync(_id, ids.Take(3).ToList(), leader, ct)).FirstError.Code);
        Assert.Equal("Resistance.WrongTeamSize", (await _roundService.SelectTeamAsync(_id, [ids[0], ids[0]], leader, ct)).FirstError.Code);
        Assert.Equal("Resistance.WrongTeamSize", (await _roundService.SelectTeamAsync(_id, null, leader, ct)).FirstError.Code);
        Assert.Equal("Resistance.UnknownPlayer", (await _roundService.SelectTeamAsync(_id, [ids[0], Guid.NewGuid()], leader, ct)).FirstError.Code);
    }

    [Fact]
    public async Task WithoutThePlotCards_TheGameShowsNoCardsAndPicksAtOnce()
    {
        var ct = CancellationToken.None;
        var callers = await OpenGameAndJoinAsync(5);
        var started = await _roundService.StartAsync(_id, Host, ct);
        var leader = callers.First(x => x.PlayerId == started.Value.LeaderPlayerId);

        Assert.Equal(ResistancePhaseDto.SelectingTeam, started.Value.Phase);
        Assert.Empty(started.Value.PlotCardCounts);
        Assert.Empty((await _lobbyService.GetAsync(_id, leader, ct)).Value.YourPlotCards);
        Assert.Equal("Resistance.NotDealing", (await _plotCardService.DealPlotCardsAsync(_id, [], leader, ct)).FirstError.Code);
    }

    [Fact]
    public async Task SelectingATeam_LetsTheLeaderPickThemselvesAndLocksTheTeam()
    {
        var ct = CancellationToken.None;
        var callers = await OpenGameAndJoinAsync(5);
        var started = await _roundService.StartAsync(_id, Host, ct);
        var leader = callers.First(x => x.PlayerId == started.Value.LeaderPlayerId);
        var other = started.Value.Players.First(x => x.Id != leader.PlayerId).Id;
        var before = _notifier.ResistanceChanges.Count;

        var result = await _roundService.SelectTeamAsync(_id, [leader.PlayerId!.Value, other], leader, ct);

        Assert.False(result.IsError);
        Assert.Equal(2, result.Value.TeamPlayerIds.Count);
        Assert.Contains(leader.PlayerId!.Value, result.Value.TeamPlayerIds);
        Assert.Contains(other, result.Value.TeamPlayerIds);
        Assert.Equal(before + 1, _notifier.ResistanceChanges.Count);
        Assert.Equal("Resistance.TeamSubmitted", (await _roundService.SelectTeamAsync(_id, [leader.PlayerId.Value, other], leader, ct)).FirstError.Code);
    }

    [Fact]
    public async Task SelectedTeam_IsVisibleToEveryone()
    {
        var ct = CancellationToken.None;
        var callers = await OpenGameAndJoinAsync(5);
        var started = await _roundService.StartAsync(_id, Host, ct);
        var leader = callers.First(x => x.PlayerId == started.Value.LeaderPlayerId);
        var team = started.Value.Players.Take(2).Select(x => x.Id).ToList();
        await _roundService.SelectTeamAsync(_id, team, leader, ct);

        var asOther = await _lobbyService.GetAsync(_id, callers.First(x => x.PlayerId != leader.PlayerId), ct);
        var asHost = await _lobbyService.GetAsync(_id, Host, ct);

        Assert.Equal(team.Order(), asOther.Value.TeamPlayerIds.Order());
        Assert.Equal(team.Order(), asHost.Value.TeamPlayerIds.Order());
    }

    [Fact]
    public async Task Voting_NeedsASubmittedTeamAndAPlayerOfTheGame()
    {
        var ct = CancellationToken.None;
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _roundService.VoteAsync(_id, true, new GameCaller("guest", false), ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.NotFound, (await _roundService.VoteAsync(_id, true, Host, ct)).FirstError.Type);

        var callers = await OpenGameAndJoinAsync(5);
        Assert.Equal("Resistance.NotStarted", (await _roundService.VoteAsync(_id, true, callers[0], ct)).FirstError.Code);

        var started = await _roundService.StartAsync(_id, Host, ct);
        Assert.Equal("Resistance.NotVoting", (await _roundService.VoteAsync(_id, true, callers[0], ct)).FirstError.Code);

        var leader = callers.First(x => x.PlayerId == started.Value.LeaderPlayerId);
        await _roundService.SelectTeamAsync(_id, started.Value.Players.Take(2).Select(x => x.Id).ToList(), leader, ct);

        var watcher = await _roundService.VoteAsync(_id, true, Host, ct);
        Assert.Equal("Resistance.NotPlayer", watcher.FirstError.Code);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, watcher.FirstError.Type);
        Assert.Equal("Resistance.NotPlayer", (await _roundService.VoteAsync(_id, true, Admin, ct)).FirstError.Code);
        Assert.False((await _roundService.VoteAsync(_id, true, callers[0], ct)).IsError);
    }

    [Fact]
    public async Task Votes_AreSecretUntilEveryoneVoted()
    {
        var ct = CancellationToken.None;
        var (callers, _) = await StartWithTeamAsync(5);
        var before = _notifier.ResistanceChanges.Count;

        await _roundService.VoteAsync(_id, true, callers[0], ct);
        await _roundService.VoteAsync(_id, false, callers[1], ct);

        var asVoter = (await _lobbyService.GetAsync(_id, callers[0], ct)).Value;
        var asOther = (await _lobbyService.GetAsync(_id, callers[2], ct)).Value;
        var asHost = (await _lobbyService.GetAsync(_id, Host, ct)).Value;

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

        await _roundService.VoteAsync(_id, true, callers[0], ct);
        var changed = await _roundService.VoteAsync(_id, false, callers[0], ct);
        Assert.False(changed.Value.YourVote);
        Assert.Single(changed.Value.VotedPlayerIds);

        foreach (var caller in callers.Skip(1))
        {
            await _roundService.VoteAsync(_id, true, caller, ct);
        }

        var late = await _roundService.VoteAsync(_id, true, callers[0], ct);
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
            var game = (await _lobbyService.GetAsync(_id, viewer, ct)).Value;
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

        var result = await _roundService.ContinueAsync(_id, leader, ct);

        Assert.Equal("Resistance.NothingToContinue", result.FirstError.Code);
        var game = (await _lobbyService.GetAsync(_id, Host, ct)).Value;
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
        Assert.Equal("Resistance.NotPlayingMission", (await _roundService.PlayMissionCardAsync(_id, true, leader, ct)).FirstError.Code);
        await VoteAsync(callers, true, true, true, true, true);

        var team = (await _lobbyService.GetAsync(_id, Host, ct)).Value.TeamPlayerIds;
        var outsider = callers.First(x => !team.Contains(x.PlayerId!.Value));

        Assert.Equal("Resistance.NotPlayer", (await _roundService.PlayMissionCardAsync(_id, true, Host, ct)).FirstError.Code);
        Assert.Equal("Resistance.NotPlayer", (await _roundService.PlayMissionCardAsync(_id, true, Admin, ct)).FirstError.Code);
        var notOnTeam = await _roundService.PlayMissionCardAsync(_id, true, outsider, ct);
        Assert.Equal("Resistance.NotOnTeam", notOnTeam.FirstError.Code);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, notOnTeam.FirstError.Type);
    }

    [Fact]
    public async Task AMemberOfTheResistance_CanOnlyPlaySuccess_AndEachCardIsPlayedOnce()
    {
        var ct = CancellationToken.None;
        var mission = await StartMissionAsync(5, spiesOnTeam: 1);

        var failed = await _roundService.PlayMissionCardAsync(_id, false, mission.Resistance[0], ct);
        Assert.Equal("Resistance.MustSucceed", failed.FirstError.Code);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, failed.FirstError.Type);

        var played = await _roundService.PlayMissionCardAsync(_id, true, mission.Resistance[0], ct);
        Assert.False(played.IsError);
        Assert.Equal("Resistance.AlreadyPlayed", (await _roundService.PlayMissionCardAsync(_id, true, mission.Resistance[0], ct)).FirstError.Code);
        Assert.False((await _roundService.PlayMissionCardAsync(_id, false, mission.Spies[0], ct)).IsError);
    }

    [Fact]
    public async Task TheCards_AreSecret_OnlyWhoPlayedIsPublic()
    {
        var ct = CancellationToken.None;
        var mission = await StartMissionAsync(5, spiesOnTeam: 1);
        var before = _notifier.ResistanceChanges.Count;

        await _roundService.PlayMissionCardAsync(_id, false, mission.Spies[0], ct);

        Assert.Equal(before + 1, _notifier.ResistanceChanges.Count);
        var asSpy = (await _lobbyService.GetAsync(_id, mission.Spies[0], ct)).Value;
        var asOther = (await _lobbyService.GetAsync(_id, mission.Resistance[0], ct)).Value;
        var asHost = (await _lobbyService.GetAsync(_id, Host, ct)).Value;

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

        await _roundService.PlayMissionCardAsync(_id, true, mission.Resistance[0], ct);
        await _roundService.PlayMissionCardAsync(_id, false, mission.Spies[0], ct);

        var game = (await _lobbyService.GetAsync(_id, Host, ct)).Value;
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
            await _roundService.PlayMissionCardAsync(_id, true, caller, ct);
        }

        var game = (await _lobbyService.GetAsync(_id, Host, ct)).Value;
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
        Assert.Equal(2, (await _lobbyService.GetAsync(_id, Host, ct)).Value.RequiredFails);

        await _roundService.PlayMissionCardAsync(_id, true, mission.Resistance[0], ct);
        await _roundService.PlayMissionCardAsync(_id, false, mission.Spies[0], ct);

        var game = (await _lobbyService.GetAsync(_id, Host, ct)).Value;
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
        var leaderId = (await _lobbyService.GetAsync(_id, Host, ct)).Value.LeaderPlayerId;
        foreach (var caller in mission.Team)
        {
            await _roundService.PlayMissionCardAsync(_id, caller.Equals(mission.Spies[0]) ? false : true, caller, ct);
        }

        var continued = (await _roundService.ContinueAsync(_id, Host, ct)).Value;

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
        await _roundService.StartAsync(_id, Host, ct);

        for (var round = 0; round < 3; round++)
        {
            await PlayRoundAsync(callers, fail: !resistanceWins);
            if (round < 2)
            {
                await _roundService.ContinueAsync(_id, Host, ct);
            }
        }

        var game = (await _lobbyService.GetAsync(_id, callers[0], ct)).Value;
        Assert.Equal(ResistancePhaseDto.GameOver, game.Phase);
        Assert.Equal(resistanceWins ? ResistanceRoleDto.Resistance : ResistanceRoleDto.Spy, game.Winner);
        Assert.Equal(3, game.MissionResults.Count(x => x.Succeeded == resistanceWins));
        Assert.Equal(5, game.RevealedRoles!.Count);
        Assert.Equal("Resistance.GameOver", (await _roundService.ContinueAsync(_id, Host, ct)).FirstError.Code);
        Assert.Equal("Resistance.GameOver", (await _roundService.PlayMissionCardAsync(_id, true, callers[0], ct)).FirstError.Code);
    }

    [Fact]
    public async Task TheGame_GoesOnAfterTwoSuccessesAndOneFailure()
    {
        var ct = CancellationToken.None;
        var callers = await OpenGameAndJoinAsync(5);
        await _roundService.StartAsync(_id, Host, ct);

        foreach (var fail in new[] { false, true, false })
        {
            await PlayRoundAsync(callers, fail);
            await _roundService.ContinueAsync(_id, Host, ct);
        }

        var game = (await _lobbyService.GetAsync(_id, Host, ct)).Value;
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

        var rejected = (await _lobbyService.GetAsync(_id, Host, ct)).Value;
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
        var started = (await _roundService.StartAsync(_id, Host, ct)).Value;
        var order = started.Players.Select(x => x.Id).ToList();
        var seen = new List<Guid?> { started.LeaderPlayerId };

        for (var i = 0; i < 4; i++)
        {
            var leader = callers.First(x => x.PlayerId == seen[^1]);
            await _roundService.SelectTeamAsync(_id, order.Take(2).ToList(), leader, ct);
            await VoteAsync(callers, false, false, false, false, false);
            seen.Add((await _lobbyService.GetAsync(_id, Host, ct)).Value.LeaderPlayerId);
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
            await _roundService.PlayMissionCardAsync(_id, !mission.Spies.Contains(caller), caller, ct);
        }

        var game = (await _lobbyService.GetAsync(_id, Host, ct)).Value;
        var other = mission.Team.First(x => x.PlayerId != game.LeaderPlayerId);
        var denied = await _roundService.ContinueAsync(_id, other, ct);
        Assert.Equal("Resistance.NotAllowed", denied.FirstError.Code);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, denied.FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _roundService.ContinueAsync(_id, new GameCaller("guest", false), ct)).FirstError.Type);
        Assert.False((await _roundService.ContinueAsync(_id, Host, ct)).IsError);
        Assert.Equal("Resistance.NothingToContinue", (await _roundService.ContinueAsync(_id, Host, ct)).FirstError.Code);
    }

    [Fact]
    public async Task FiveRejectedTeamsInARow_GiveTheSpiesTheWinAndEndTheRound()
    {
        var ct = CancellationToken.None;
        var callers = await OpenGameAndJoinAsync(5);
        var started = (await _roundService.StartAsync(_id, Host, ct)).Value;
        var team = started.Players.Take(2).Select(x => x.Id).ToList();
        var leaderId = started.LeaderPlayerId;

        for (var i = 1; i <= 5; i++)
        {
            await _roundService.SelectTeamAsync(_id, team, callers.First(x => x.PlayerId == leaderId), ct);
            await VoteAsync(callers, false, false, false, false, false);
            if (i < 5)
            {
                Assert.Equal(i, (await _lobbyService.GetAsync(_id, Host, ct)).Value.RejectedTeams);
                leaderId = (await _lobbyService.GetAsync(_id, Host, ct)).Value.LeaderPlayerId;
            }
        }

        var over = (await _lobbyService.GetAsync(_id, callers[0], ct)).Value;
        Assert.Equal(ResistancePhaseDto.GameOver, over.Phase);
        Assert.Equal(ResistanceRoleDto.Spy, over.Winner);
        Assert.Equal(5, over.RejectedTeams);
        Assert.Equal(5, over.Votes!.Count);
        Assert.Equal(5, over.RevealedRoles!.Count);
        Assert.Equal("Resistance.GameOver", (await _roundService.ContinueAsync(_id, Host, ct)).FirstError.Code);
        Assert.Equal("Resistance.GameOver", (await _roundService.VoteAsync(_id, true, callers[0], ct)).FirstError.Code);
        Assert.Equal("Resistance.GameOver", (await _roundService.SelectTeamAsync(_id, team, callers.First(x => x.PlayerId == leaderId), ct)).FirstError.Code);
    }

    private async Task<List<GameCaller>> EndGameAsync()
    {
        var ct = CancellationToken.None;
        var callers = await OpenGameAndJoinAsync(5);
        var started = (await _roundService.StartAsync(_id, Host, ct)).Value;
        var team = started.Players.Take(2).Select(x => x.Id).ToList();
        var leaderId = started.LeaderPlayerId;
        for (var i = 1; i <= 5; i++)
        {
            await _roundService.SelectTeamAsync(_id, team, callers.First(x => x.PlayerId == leaderId), ct);
            await VoteAsync(callers, false, false, false, false, false);
            leaderId = (await _lobbyService.GetAsync(_id, Host, ct)).Value.LeaderPlayerId;
        }

        return callers;
    }

    [Fact]
    public async Task Restart_AfterTheGameEnded_ReturnsToTheOpenLobby()
    {
        var ct = CancellationToken.None;
        var callers = await EndGameAsync();

        var before = _notifier.ResistanceChanges.Count;
        var restarted = await _roundService.RestartAsync(_id, Host, ct);

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
        Assert.All(callers, x => Assert.Null(_lobbyService.GetAsync(_id, x, ct).Result.Value.YourRole));
        Assert.Equal(before + 1, _notifier.ResistanceChanges.Count);
    }

    [Fact]
    public async Task Restart_WhileTheGameIsOn_Conflicts()
    {
        await StartWithTeamAsync(5);

        var result = await _roundService.RestartAsync(_id, Host, CancellationToken.None);

        Assert.Equal("Resistance.NotOver", result.FirstError.Code);
    }

    [Fact]
    public async Task Restart_BeforeTheStart_Conflicts()
    {
        await OpenGameAndJoinAsync(5);

        var result = await _roundService.RestartAsync(_id, Host, CancellationToken.None);

        Assert.Equal("Resistance.NotStarted", result.FirstError.Code);
    }

    [Fact]
    public async Task Restart_IsNotForPlayers()
    {
        var callers = await EndGameAsync();

        var result = await _roundService.RestartAsync(_id, callers[0], CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
    }

    [Fact]
    public async Task AnApproval_ResetsTheRejectionCount()
    {
        var ct = CancellationToken.None;
        var (callers, leader) = await StartWithTeamAsync(5);
        await VoteAsync(callers, false, false, false, false, false);
        var next = (await _lobbyService.GetAsync(_id, Host, ct)).Value;
        var team = next.Players.Take(2).Select(x => x.Id).ToList();
        await _roundService.SelectTeamAsync(_id, team, callers.First(x => x.PlayerId == next.LeaderPlayerId), ct);

        await VoteAsync(callers, true, true, true, true, true);

        Assert.NotEqual(leader.PlayerId, next.LeaderPlayerId);
        Assert.Equal(0, (await _lobbyService.GetAsync(_id, Host, ct)).Value.RejectedTeams);
    }

    [Fact]
    public async Task Roles_StayHiddenWhileTheGameIsOn()
    {
        var (callers, _) = await StartWithTeamAsync(5);
        await VoteAsync(callers, false, false, false, false, false);

        var game = (await _lobbyService.GetAsync(_id, callers[0], CancellationToken.None)).Value;

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
        await _roundService.StartAsync(_id, Host, CancellationToken.None);

        var game = await _games.GetAsync(_id, CancellationToken.None);

        Assert.Equal(spies, game!.Players.Count(x => x.Role == ResistanceRole.Spy));
        Assert.Equal(players - spies, game.Players.Count(x => x.Role == ResistanceRole.Resistance));
        Assert.Equal(callers.Count, game.Players.Count);
    }

    [Fact]
    public async Task AfterStarting_EachPlayerSeesOnlyTheirOwnRoleAndSpiesSeeEachOther()
    {
        await OpenGameAsync();
        var callers = await JoinPlayersAsync(7);
        await _roundService.StartAsync(_id, Host, CancellationToken.None);
        var game = await _games.GetAsync(_id, CancellationToken.None);

        foreach (var caller in callers)
        {
            var dto = (await _lobbyService.GetAsync(_id, caller, CancellationToken.None)).Value;
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
        await _roundService.StartAsync(_id, Host, CancellationToken.None);

        foreach (var observer in new[] { Admin, Host, new GameCaller("guest", false) })
        {
            var dto = (await _lobbyService.GetAsync(_id, observer, CancellationToken.None)).Value;

            Assert.Null(dto.YourRole);
            Assert.Null(dto.FellowSpies);
            Assert.NotNull(dto.LeaderPlayerId);
        }
    }
}
