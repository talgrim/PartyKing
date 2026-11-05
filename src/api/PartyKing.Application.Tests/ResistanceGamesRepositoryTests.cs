using PartyKing.Application.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Domain.Entities.Games.Resistance;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Infrastructure.Metrics;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Application.Tests;

/// <summary>
/// Runs the voting against a real SQLite file in a fresh temp directory per test, so the migration, the persisted
/// columns and the tally in one save are covered, which the in-memory fake in the service tests is not.
/// </summary>
public class ResistanceGamesRepositoryTests : IAsyncLifetime
{
    private readonly RepositoryMetrics _metrics = new();
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"partyking-tests-{Guid.NewGuid():N}");
    private string ConnectionString => $"Data Source={Path.Combine(_directory, "PartyKing.db")}";
    private ServiceProvider _provider = null!;
    private ResistanceGamesRepository _repository = null!;
    private Guid _sessionId;
    private List<Guid> _players = [];

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);

        _provider = new ServiceCollection()
            .AddDbContextFactory<WriterDbContext>(options => options.UseSqlite(ConnectionString))
            .AddDbContextFactory<ReaderDbContext>(options => options.UseSqlite(ConnectionString))
            .BuildServiceProvider();

        var writer = _provider.GetRequiredService<IDbContextFactory<WriterDbContext>>();
        await using (var dbContext = await writer.CreateDbContextAsync())
        {
            await dbContext.Database.MigrateAsync();

            var session = new Session(Guid.NewGuid(), "ABC123", "Party", "hash", DateTime.UtcNow);
            _sessionId = session.Id;
            dbContext.Sessions.Add(session);
            await dbContext.SaveChangesAsync();
        }

        _repository = new ResistanceGamesRepository(
            _provider.GetRequiredService<IDbContextFactory<ReaderDbContext>>(),
            writer, _metrics, new NullDomainEventDispatcher());
    }

    public async Task DisposeAsync()
    {
        _metrics.Dispose();
        await _provider.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    private async Task<ResistanceGame> StartGameWithTeamAsync(int playerCount)
    {
        var ct = CancellationToken.None;
        var game = new ResistanceGame { SessionId = _sessionId, CreatedAt = DateTimeOffset.UtcNow, IsOpen = true };
        for (var i = 0; i < playerCount; i++)
        {
            game.Players.Add(new ResistancePlayer
            {
                SessionId = _sessionId,
                PlayerId = Guid.NewGuid(),
                Name = $"Player {i}",
                NormalizedName = $"player {i}",
                Order = i,
            });
        }

        Assert.True(await _repository.TryCreateAsync(game, ct));
        var started = await StartAsync();
        Assert.Null(started.Failure);

        _players = started.Game!.Players.OrderBy(x => x.Order).Select(x => x.PlayerId).ToList();
        var team = _players.Take(ResistanceRules.Default().TeamSize(playerCount, 1)).ToList();
        var selected = await SelectTeamAsync(started.Game.LeaderPlayerId, team);
        Assert.Null(selected.Failure);

        return (await _repository.GetAsync(_sessionId, ct))!;
    }

    private async Task VoteAsync(params bool[] approvals)
    {
        for (var i = 0; i < approvals.Length; i++)
        {
            var result = await CastVoteAsync(_players[i], approvals[i]);
            Assert.Null(result.Failure);
        }
    }

    private Task<ResistanceOutcome> CastVoteAsync(Guid? player, bool approve)
    {
        return _repository.UpdateAsync(
            _sessionId,
            "VoteAsync",
            ResistanceOutcome.NoGame,
            game => game.Vote(player, approve),
            CancellationToken.None);
    }

    private Task<ResistanceOutcome> SelectTeamAsync(Guid? leader, IReadOnlyList<Guid> playerIds)
    {
        return _repository.UpdateAsync(
            _sessionId,
            "SelectTeamAsync",
            ResistanceOutcome.NoGame,
            game => game.SelectTeam(leader, playerIds),
            CancellationToken.None);
    }

    private Task<ResistanceOutcome> PlayMissionCardAsync(Guid? player, bool success)
    {
        return _repository.UpdateAsync(
            _sessionId,
            "PlayMissionCardAsync",
            ResistanceOutcome.NoGame,
            game => game.PlayMissionCard(player, success),
            CancellationToken.None);
    }

    private Task<ResistanceOutcome> ContinueAsync(Guid? caller, bool isManager)
    {
        return _repository.UpdateAsync(
            _sessionId,
            "ContinueAsync",
            ResistanceOutcome.NoGame,
            game => game.Continue(caller, isManager),
            CancellationToken.None);
    }

    private Task<ResistanceOutcome> StartAsync()
    {
        return _repository.UpdateAsync(
            _sessionId,
            "StartAsync",
            ResistanceOutcome.NoGame,
            game => game.Start(DateTimeOffset.UtcNow, Random.Shared),
            CancellationToken.None);
    }

    private Task<ResistanceOutcome> RestartAsync()
    {
        return _repository.UpdateAsync(
            _sessionId,
            "RestartAsync",
            ResistanceOutcome.NoGame,
            game => game.Restart(),
            CancellationToken.None);
    }

    private Task<ResistanceOutcome> SetPlotCardsAsync(bool enabled)
    {
        return _repository.UpdateAsync(
            _sessionId,
            "SetPlotCardsAsync",
            ResistanceOutcome.NoGame,
            game => game.SetPlotCards(enabled),
            CancellationToken.None);
    }

    private async Task<ResistanceGame> ReloadAsync() => (await _repository.GetAsync(_sessionId, CancellationToken.None))!;

    [Fact]
    public async Task SubmittingTheTeam_MovesTheGameToVoting()
    {
        var game = await StartGameWithTeamAsync(5);

        Assert.Equal(ResistancePhase.Voting, game.Phase);
        Assert.Equal(2, game.Players.Count(x => x.IsOnTeam));
        Assert.All(game.Players, x => Assert.Null(x.Vote));
    }

    [Fact]
    public async Task AVote_IsPersistedAndCanBeReplacedUntilTheLastPlayerVoted()
    {
        await StartGameWithTeamAsync(5);

        await VoteAsync(true);
        await VoteAsync(false);

        var game = await ReloadAsync();
        Assert.Equal(ResistancePhase.Voting, game.Phase);
        Assert.False(game.Players.Single(x => x.PlayerId == _players[0]).Vote);
        Assert.Single(game.Players, x => x.Vote is not null);
    }

    [Fact]
    public async Task OnlyAPlayerOfTheGame_VotesOnASubmittedTeam()
    {
        var ct = CancellationToken.None;
        await StartGameWithTeamAsync(5);

        Assert.Equal(ResistanceFailure.NotPlayerToVote, (await CastVoteAsync(null, true)).Failure);
        Assert.Equal(ResistanceFailure.NotPlayerToVote, (await CastVoteAsync(Guid.NewGuid(), true)).Failure);
        Assert.Equal(ResistanceFailure.NoGame, (await _repository.UpdateAsync(Guid.NewGuid(), "VoteAsync", ResistanceOutcome.NoGame, game => game.Vote(_players[0], true), ct)).Failure);
    }

    [Fact]
    public async Task TheLastVote_ApprovesByMajorityAndKeepsTeamAndVotes()
    {
        await StartGameWithTeamAsync(5);

        await VoteAsync(true, true, true, false, false);

        var game = await ReloadAsync();
        Assert.Equal(ResistancePhase.PlayingMission, game.Phase);
        Assert.Equal(0, game.RejectedTeams);
        Assert.Equal(2, game.Players.Count(x => x.IsOnTeam));
        Assert.All(game.Players, x => Assert.NotNull(x.Vote));
        Assert.Equal(ResistanceFailure.NotVoting, (await CastVoteAsync(_players[0], true)).Failure);
    }

    [Fact]
    public async Task ATiedVote_RejectsTheTeam()
    {
        await StartGameWithTeamAsync(6);

        await VoteAsync(true, true, true, false, false, false);

        var game = await ReloadAsync();
        Assert.Equal(ResistancePhase.SelectingTeam, game.Phase);
        Assert.Equal(1, game.RejectedTeams);
        Assert.False(ResistanceRoles.IsApproved(game));
    }

    [Fact]
    public async Task ARejectedTeam_PassesTheLeadershipAtOnce_ClearsTheTeamAndKeepsTheVotes()
    {
        var before = await StartGameWithTeamAsync(5);

        await VoteAsync(false, false, false, false, false);

        var game = await ReloadAsync();
        var expected = _players[(_players.IndexOf(before.LeaderPlayerId!.Value) + 1) % 5];
        Assert.Equal(expected, game.LeaderPlayerId);
        Assert.Equal(ResistancePhase.SelectingTeam, game.Phase);
        Assert.Equal(1, game.RejectedTeams);
        Assert.All(game.Players, x =>
        {
            Assert.False(x.IsOnTeam);
            Assert.False(x.Vote);
        });
    }

    [Fact]
    public async Task SubmittingTheNextTeam_ClearsTheVotesOfTheRejectedOne()
    {
        var ct = CancellationToken.None;
        await StartGameWithTeamAsync(5);
        await VoteAsync(false, false, false, false, false);
        var leader = (await ReloadAsync()).LeaderPlayerId;

        await SelectTeamAsync(leader, _players.Take(2).ToList());

        var game = await ReloadAsync();
        Assert.Equal(ResistancePhase.Voting, game.Phase);
        Assert.All(game.Players, x => Assert.Null(x.Vote));
    }

    [Fact]
    public async Task Continuing_NeedsTheLeaderOrAManagerAndAFinishedMission()
    {
        var ct = CancellationToken.None;
        var game = await StartGameWithTeamAsync(5);
        var notLeader = _players.First(x => x != game.LeaderPlayerId);

        Assert.Equal(ResistanceFailure.NothingToContinue, (await ContinueAsync(game.LeaderPlayerId, false)).Failure);
        await VoteAsync(false, false, false, false, false);
        Assert.Equal(ResistanceFailure.NothingToContinue, (await ContinueAsync(null, true)).Failure);
        notLeader = _players.First(x => x != (ReloadAsync().Result).LeaderPlayerId);
        Assert.Equal(ResistanceFailure.NotAllowedToContinue, (await ContinueAsync(notLeader, false)).Failure);
    }

    [Fact]
    public async Task AnApprovedTeam_CannotBeContinuedFromWhileItPlaysTheMission()
    {
        await StartGameWithTeamAsync(5);
        await VoteAsync(true, true, true, true, true);

        var result = await ContinueAsync(null, isManager: true);

        Assert.Equal(ResistanceFailure.NothingToContinue, result.Failure);
    }

    [Fact]
    public async Task TheLastMissionCard_PersistsTheResultAndContinueMovesToTheNextMission()
    {
        var ct = CancellationToken.None;
        await StartGameWithTeamAsync(5);
        await VoteAsync(true, true, true, true, true);
        var team = (await ReloadAsync()).Players.Where(x => x.IsOnTeam).ToList();
        // Every spy on the team plays Fail (both of them do when the random team of two is all spies)
        var spies = team.Count(x => x.Role == ResistanceRole.Spy);

        foreach (var member in team)
        {
            var success = member.Role == ResistanceRole.Resistance;
            var played = await PlayMissionCardAsync(member.PlayerId, success);
            Assert.Null(played.Failure);
        }

        var game = await ReloadAsync();
        Assert.Equal(ResistancePhase.MissionResult, game.Phase);
        var result = Assert.Single(game.MissionResults);
        Assert.Equal(1, result.MissionNumber);
        Assert.Equal(spies == 0, result.Succeeded);
        Assert.Equal(spies, result.FailCount);

        var next = await ContinueAsync(null, isManager: true);
        Assert.Null(next.Failure);
        var after = await ReloadAsync();
        Assert.Equal(ResistancePhase.SelectingTeam, after.Phase);
        Assert.Equal(2, after.MissionNumber);
        Assert.All(after.Players, x => Assert.Null(x.MissionCard));
        Assert.Single(after.MissionResults);
    }

    [Fact]
    public async Task Restart_AfterTheGameEnded_ClearsTheResultsAndReturnsToTheLobby()
    {
        var ct = CancellationToken.None;
        await StartGameWithTeamAsync(5);
        await VoteAsync(true, true, true, true, true);
        foreach (var member in (await ReloadAsync()).Players.Where(x => x.IsOnTeam).ToList())
        {
            await PlayMissionCardAsync(member.PlayerId, member.Role == ResistanceRole.Resistance);
        }

        await ContinueAsync(null, isManager: true);
        Assert.Equal(ResistanceFailure.NotOver, (await RestartAsync()).Failure);

        for (var i = 1; i <= 5; i++)
        {
            var current = await ReloadAsync();
            var size = current.Rules.TeamSize(5, current.MissionNumber);
            Assert.Null((await SelectTeamAsync(current.LeaderPlayerId, _players.Take(size).ToList())).Failure);
            await VoteAsync(false, false, false, false, false);
        }

        Assert.Equal(ResistancePhase.GameOver, (await ReloadAsync()).Phase);
        var result = await RestartAsync();

        Assert.Null(result.Failure);
        var game = await ReloadAsync();
        Assert.Equal(ResistancePhase.SelectingTeam, game.Phase);
        Assert.Equal(1, game.MissionNumber);
        Assert.Equal(0, game.RejectedTeams);
        Assert.Null(game.Winner);
        Assert.Empty(game.MissionResults);
        Assert.Equal(5, game.Players.Count);
        Assert.All(game.Players, x =>
        {
            Assert.False(x.IsOnTeam);
            Assert.Null(x.Vote);
            Assert.Null(x.MissionCard);
        });
        Assert.All(game.Players, x => Assert.Null(x.Role));
        Assert.Null(game.LeaderPlayerId);
        Assert.Null(game.StartedAt);
        Assert.True(game.IsOpen);

        var started = await StartAsync();

        Assert.Null(started.Failure);
        Assert.Equal(2, (await ReloadAsync()).Players.Count(x => x.Role == ResistanceRole.Spy));
    }

    [Fact]
    public async Task PlotCards_CanBeSwitchedInTheOpenLobbyButNotOnceTheGameStarted()
    {
        var ct = CancellationToken.None;
        await StartGameWithTeamAsync(5);

        Assert.Equal(ResistanceFailure.PlotCardsLocked, (await SetPlotCardsAsync(true)).Failure);

        for (var i = 1; i <= 5; i++)
        {
            var current = await ReloadAsync();
            var size = current.Rules.TeamSize(5, current.MissionNumber);
            await SelectTeamAsync(current.LeaderPlayerId, _players.Take(size).ToList());
            await VoteAsync(false, false, false, false, false);
        }

        await RestartAsync();

        Assert.Null((await SetPlotCardsAsync(true)).Failure);
        Assert.True((await ReloadAsync()).Rules.PlotCardsEnabled);
    }

    [Fact]
    public async Task TheInquisitor_SurvivesReloadsThroughMissionTwoAndTheExamination()
    {
        await CreateLobbyAsync(5, isOpen: true);
        var switched = await _repository.UpdateAsync(
            _sessionId, "SetInquisitorAsync", ResistanceOutcome.NoGame, game => game.SetInquisitor(true), CancellationToken.None);
        Assert.Null(switched.Failure);
        Assert.True((await ReloadAsync()).Rules.InquisitorEnabled);

        Assert.Null((await StartAsync()).Failure);
        var started = await ReloadAsync();
        _players = started.Players.OrderBy(x => x.Order).Select(x => x.PlayerId).ToList();
        var holder = started.InquisitorPlayerId!.Value;
        Assert.Equal([holder], started.Players.Where(x => x.WasInquisitor).Select(x => x.PlayerId));

        await PlayApprovedMissionAsync();
        Assert.Equal(ResistancePhase.MissionResult, (await ReloadAsync()).Phase);
        Assert.Null((await ContinueAsync((await ReloadAsync()).LeaderPlayerId, isManager: true)).Failure);
        await PlayApprovedMissionAsync();

        var inquisition = await ReloadAsync();
        Assert.Equal(ResistancePhase.Inquisition, inquisition.Phase);
        var target = _players.First(x => x != holder);
        var examined = await _repository.UpdateAsync(
            _sessionId, "ExamineAsync", ResistanceOutcome.NoGame, game => game.Examine(holder, target), CancellationToken.None);
        Assert.Null(examined.Failure);

        var after = await ReloadAsync();
        Assert.Equal(target, after.InquisitorPlayerId);
        Assert.Equal(holder, after.InquisitionExaminerPlayerId);
        Assert.Equal(target, after.InquisitionExaminedPlayerId);
        Assert.Equal(2, after.Players.Count(x => x.WasInquisitor));

        var confirmed = await _repository.UpdateAsync(
            _sessionId, "ConfirmInquisitionAsync", ResistanceOutcome.NoGame, game => game.ConfirmInquisition(holder), CancellationToken.None);
        Assert.Null(confirmed.Failure);
        var done = await ReloadAsync();
        Assert.Equal(ResistancePhase.MissionResult, done.Phase);
        Assert.Null(done.InquisitionExaminerPlayerId);
        Assert.Equal(target, done.InquisitorPlayerId);
    }

    /// <summary>The leader picks a team that everybody approves and whose members all play Success.</summary>
    private async Task PlayApprovedMissionAsync()
    {
        var current = await ReloadAsync();
        var size = current.Rules.TeamSize(5, current.MissionNumber);
        var team = _players.Take(size).ToList();
        Assert.Null((await SelectTeamAsync(current.LeaderPlayerId, team)).Failure);
        await VoteAsync(true, true, true, true, true);
        foreach (var member in team)
        {
            Assert.Null((await PlayMissionCardAsync(member, success: true)).Failure);
        }
    }

    [Fact]
    public async Task TheFifthRejectedTeamInARow_EndsTheGameWithTheSpiesWinning()
    {
        var ct = CancellationToken.None;
        await StartGameWithTeamAsync(5);

        for (var i = 1; i <= 5; i++)
        {
            await VoteAsync(false, false, false, false, false);
            if (i < 5)
            {
                var game = await ReloadAsync();
                Assert.Equal(i, game.RejectedTeams);
                game = await ReloadAsync();
                var team = _players.Take(2).ToList();
                Assert.Null((await SelectTeamAsync(game.LeaderPlayerId, team)).Failure);
            }
        }

        var over = await ReloadAsync();
        Assert.Equal(ResistancePhase.GameOver, over.Phase);
        Assert.Equal(ResistanceRole.Spy, over.Winner);
        Assert.Equal(5, over.RejectedTeams);
        Assert.All(over.Players, x => Assert.NotNull(x.Vote));
        Assert.Equal(ResistanceFailure.GameOver, (await CastVoteAsync(_players[0], true)).Failure);
        Assert.Equal(ResistanceFailure.GameOver, (await ContinueAsync(null, true)).Failure);
        Assert.Equal(ResistanceFailure.GameOver, (await SelectTeamAsync(over.LeaderPlayerId, _players.Take(2).ToList())).Failure);
    }

    [Fact]
    public async Task AnApproval_ResetsTheRejectionCount()
    {
        var ct = CancellationToken.None;
        await StartGameWithTeamAsync(5);
        await VoteAsync(false, false, false, false, false);
        await ContinueAsync(null, true);
        var game = await ReloadAsync();
        await SelectTeamAsync(game.LeaderPlayerId, _players.Take(2).ToList());

        await VoteAsync(true, true, true, true, true);

        Assert.Equal(0, (await ReloadAsync()).RejectedTeams);
    }

    [Fact]
    public async Task AfterARejectedTeam_OnlyTheNewLeaderCanSelect()
    {
        var ct = CancellationToken.None;
        var game = await StartGameWithTeamAsync(5);
        await VoteAsync(false, false, false, false, false);
        var next = (await ReloadAsync()).LeaderPlayerId;

        var old = await SelectTeamAsync(game.LeaderPlayerId, _players.Take(2).ToList());
        var current = await SelectTeamAsync(next, _players.Take(2).ToList());

        Assert.Equal(ResistanceFailure.NotLeaderToPick, old.Failure);
        Assert.Null(current.Failure);
    }

    /// <summary>
    /// A vote is saved together with the tally of the last one, with no concurrency token. Votes sent at the same time
    /// can make the voters that finish together each see a missing vote, so the tally never runs. This pins the
    /// invariant that must hold either way: every vote that was accepted is stored.
    /// </summary>
    [Fact]
    public async Task ConcurrentVotes_AreAllStored()
    {
        await StartGameWithTeamAsync(5);

        var statuses = await Task.WhenAll(_players.Select(player => Task.Run(
            async () => (await CastVoteAsync(player, true)).Failure)));

        var game = await ReloadAsync();
        Assert.All(statuses, x => Assert.True(x is null or ResistanceFailure.NotVoting));
        Assert.All(game.Players, x => Assert.True(x.Vote));
    }

    private async Task<ResistanceGame> CreateLobbyAsync(int playerCount, bool isOpen)
    {
        var game = new ResistanceGame { SessionId = _sessionId, CreatedAt = DateTimeOffset.UtcNow, IsOpen = isOpen };
        for (var i = 0; i < playerCount; i++)
        {
            game.Players.Add(new ResistancePlayer
            {
                SessionId = _sessionId,
                PlayerId = Guid.NewGuid(),
                Name = $"Player {i}",
                NormalizedName = $"player {i}",
                Order = i,
            });
        }

        Assert.True(await _repository.TryCreateAsync(game, CancellationToken.None));

        return game;
    }

    [Fact]
    public async Task TheLobby_IsNullWhenTheSessionHasNoGame()
    {
        var lobby = await _repository.GetLobbyAsync(_sessionId, Guid.NewGuid(), null, CancellationToken.None);

        Assert.Null(lobby);
    }

    [Fact]
    public async Task TheLobby_ShowsTheCreationTimeAndWhetherTheGameIsOpen()
    {
        var game = await CreateLobbyAsync(2, isOpen: false);

        var closed = await _repository.GetLobbyAsync(_sessionId, null, null, CancellationToken.None);
        await _repository.OpenAsync(_sessionId, CancellationToken.None);
        var open = await _repository.GetLobbyAsync(_sessionId, null, null, CancellationToken.None);

        Assert.NotNull(closed);
        Assert.False(closed.IsOpen);
        Assert.Equal(game.CreatedAt.ToUniversalTime(), closed.CreatedAt.ToUniversalTime());
        Assert.True(open!.IsOpen);
    }

    [Fact]
    public async Task TheLobby_IsJoinedOnlyForAPlayerOfTheGame()
    {
        var game = await CreateLobbyAsync(2, isOpen: true);
        var player = game.Players[0].PlayerId;

        Assert.True((await _repository.GetLobbyAsync(_sessionId, player, null, CancellationToken.None))!.Joined);
        Assert.False((await _repository.GetLobbyAsync(_sessionId, null, null, CancellationToken.None))!.Joined);
        Assert.False((await _repository.GetLobbyAsync(_sessionId, Guid.NewGuid(), null, CancellationToken.None))!.Joined);
    }

    [Fact]
    public async Task TheLobby_IsJoinedOnlyWithTheStampOfTheSeat()
    {
        var game = await CreateLobbyAsync(2, isOpen: true);
        var player = game.Players[0].PlayerId;
        var stamp = Guid.NewGuid();
        await _repository.UpdateAsync(
            _sessionId,
            "test",
            ResistanceOutcome.NoGame,
            x =>
            {
                x.Players.Single(p => p.PlayerId == player).ClaimStamp = stamp;

                return ResistanceOutcome.Success(x);
            },
            CancellationToken.None);

        Assert.True((await _repository.GetLobbyAsync(_sessionId, player, stamp, CancellationToken.None))!.Joined);
        Assert.False((await _repository.GetLobbyAsync(_sessionId, player, null, CancellationToken.None))!.Joined);
        Assert.False((await _repository.GetLobbyAsync(_sessionId, player, Guid.NewGuid(), CancellationToken.None))!.Joined);
    }

    [Fact]
    public async Task ASeat_KeepsItsReleaseStampAndAdminFlagInTheDatabase()
    {
        await CreateLobbyAsync(2, isOpen: true);
        var admin = ResistancePlayer.Create(Guid.NewGuid(), "Ola", null, DateTimeOffset.UtcNow, isAdminSeat: true);
        await _repository.JoinAsync(_sessionId, admin, CancellationToken.None);
        var seat = (await _repository.GetAsync(_sessionId, CancellationToken.None))!.Players.First(x => x.Name == "Player 0").PlayerId;
        var stamp = Guid.NewGuid();
        await _repository.UpdateAsync(
            _sessionId,
            "test",
            ResistanceOutcome.NoGame,
            x =>
            {
                x.StartedAt = DateTimeOffset.UtcNow;
                var outcome = x.SetSeatReleased(seat, true, playerOnline: false);
                Assert.Null(outcome.Failure);

                return outcome;
            },
            CancellationToken.None);

        var released = (await _repository.GetAsync(_sessionId, CancellationToken.None))!;
        await _repository.UpdateAsync(
            _sessionId,
            "test",
            ResistanceOutcome.NoGame,
            x => x.ClaimSeat(seat, playerOnline: false, stamp),
            CancellationToken.None);
        var claimed = (await _repository.GetAsync(_sessionId, CancellationToken.None))!;

        Assert.True(released.Players.Single(x => x.PlayerId == seat).IsReclaimable);
        Assert.True(released.Players.Single(x => x.PlayerId == admin.PlayerId).IsAdminSeat);
        Assert.False(released.Players.Single(x => x.PlayerId == seat).IsAdminSeat);
        Assert.False(claimed.Players.Single(x => x.PlayerId == seat).IsReclaimable);
        Assert.Equal(stamp, claimed.Players.Single(x => x.PlayerId == seat).ClaimStamp);
    }

    [Fact]
    public async Task TheLobby_DoesNotCountAPlayerOfAnotherSessionsGame()
    {
        var game = await CreateLobbyAsync(2, isOpen: true);
        var player = game.Players[0].PlayerId;
        await NewSessionAsync("OTHER1");
        await CreateLobbyAsync(2, isOpen: true);

        var lobby = await _repository.GetLobbyAsync(_sessionId, player, null, CancellationToken.None);

        Assert.False(lobby!.Joined);
    }

    [Fact]
    public async Task TheLobby_SaysWhenTheGameStarted()
    {
        await CreateLobbyAsync(5, isOpen: true);
        Assert.False((await _repository.GetLobbyAsync(_sessionId, null, null, CancellationToken.None))!.IsStarted);

        var started = await StartAsync();
        Assert.Null(started.Failure);
        Assert.True((await _repository.GetLobbyAsync(_sessionId, null, null, CancellationToken.None))!.IsStarted);
    }

    /// <summary>
    /// The projection repeats the rule of <see cref="PartyKing.Application.Games.Services.Resistance.ResistanceGameLobby.IsPlayer"/>
    /// and the old lobby read (the whole game, then the caller's id against its players). It has to answer the same.
    /// </summary>
    [Fact]
    public async Task TheLobby_AnswersLikeTheGameItIsAProjectionOf()
    {
        await CreateLobbyAsync(5, isOpen: true);
        await StartAsync();
        var game = await ReloadAsync();
        Guid?[] callers = [null, Guid.NewGuid(), .. game.Players.Select(x => (Guid?)x.PlayerId)];

        foreach (var playerId in callers)
        {
            var lobby = await _repository.GetLobbyAsync(_sessionId, playerId, null, CancellationToken.None);
            var caller = new Application.Games.Services.Base.GameCaller(null, false, playerId);

            Assert.Equal(game.IsOpen, lobby!.IsOpen);
            Assert.Equal(game.IsStarted, lobby.IsStarted);
            Assert.Equal(game.CreatedAt.ToUniversalTime(), lobby.CreatedAt.ToUniversalTime());
            Assert.Equal(
                Application.Games.Services.Resistance.ResistanceGameLobby.IsPlayer(game, caller), lobby.Joined);
        }
    }

    private async Task NewSessionAsync(string code)
    {
        await using var dbContext = await _provider.GetRequiredService<IDbContextFactory<WriterDbContext>>().CreateDbContextAsync();
        var session = new Session(Guid.NewGuid(), code, "Party", "hash", DateTime.UtcNow);
        dbContext.Sessions.Add(session);
        await dbContext.SaveChangesAsync();
        _sessionId = session.Id;
    }

    /// <summary>
    /// The vote that completes the round tallies it. Votes that land together must not each see a missing one, or the
    /// game would sit in voting with everybody's vote cast. Several games, so a rare interleaving shows up.
    /// </summary>
    [Fact]
    public async Task ConcurrentVotes_AlwaysTallyTheRound()
    {
        for (var round = 0; round < 30; round++)
        {
            await NewSessionAsync($"R{round:D5}");
            await StartGameWithTeamAsync(5);

            await Task.WhenAll(_players.Select(player => Task.Run(
                () => CastVoteAsync(player, true))));

            var game = await ReloadAsync();
            Assert.True(game.Phase != ResistancePhase.Voting, $"The round {round} was never tallied");
            Assert.All(game.Players, x => Assert.True(x.Vote));
        }
    }
}
