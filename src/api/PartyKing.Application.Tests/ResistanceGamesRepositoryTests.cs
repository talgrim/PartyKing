using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Domain.Entities.Games;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Application.Tests;

/// <summary>
/// Runs the voting against a real SQLite file in a fresh temp directory per test, so the migration, the persisted
/// columns and the tally in one save are covered, which the in-memory fake in the service tests is not.
/// </summary>
public class ResistanceGamesRepositoryTests : IAsyncLifetime
{
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

            var session = new Session("ABC123", "Party", "hash");
            _sessionId = session.Id;
            dbContext.Sessions.Add(session);
            await dbContext.SaveChangesAsync();
        }

        _repository = new ResistanceGamesRepository(
            _provider.GetRequiredService<IDbContextFactory<ReaderDbContext>>(),
            writer);
    }

    public async Task DisposeAsync()
    {
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
        var started = await _repository.StartAsync(_sessionId, DateTimeOffset.UtcNow, ct);
        Assert.Equal(ResistanceStartStatus.Started, started.Status);

        _players = started.Game!.Players.OrderBy(x => x.Order).Select(x => x.PlayerId).ToList();
        var team = _players.Take(ResistanceRoles.TeamSize(playerCount, 1)).ToList();
        var selected = await _repository.SelectTeamAsync(_sessionId, started.Game.LeaderPlayerId, team, ct);
        Assert.Equal(ResistanceTeamStatus.Selected, selected.Status);

        return (await _repository.GetAsync(_sessionId, ct))!;
    }

    private async Task VoteAsync(params bool[] approvals)
    {
        for (var i = 0; i < approvals.Length; i++)
        {
            var result = await _repository.VoteAsync(_sessionId, _players[i], approvals[i], CancellationToken.None);
            Assert.Equal(ResistanceVoteStatus.Voted, result.Status);
        }
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

        Assert.Equal(ResistanceVoteStatus.NotPlayer, (await _repository.VoteAsync(_sessionId, null, true, ct)).Status);
        Assert.Equal(ResistanceVoteStatus.NotPlayer, (await _repository.VoteAsync(_sessionId, Guid.NewGuid(), true, ct)).Status);
        Assert.Equal(ResistanceVoteStatus.NoGame, (await _repository.VoteAsync(Guid.NewGuid(), _players[0], true, ct)).Status);
    }

    [Fact]
    public async Task TheLastVote_ApprovesByMajorityAndKeepsTeamAndVotes()
    {
        await StartGameWithTeamAsync(5);

        await VoteAsync(true, true, true, false, false);

        var game = await ReloadAsync();
        Assert.Equal(ResistancePhase.VoteResult, game.Phase);
        Assert.Equal(0, game.RejectedTeams);
        Assert.Equal(2, game.Players.Count(x => x.IsOnTeam));
        Assert.All(game.Players, x => Assert.NotNull(x.Vote));
        Assert.Equal(ResistanceVoteStatus.NotVoting, (await _repository.VoteAsync(_sessionId, _players[0], true, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task ATiedVote_RejectsTheTeam()
    {
        await StartGameWithTeamAsync(6);

        await VoteAsync(true, true, true, false, false, false);

        var game = await ReloadAsync();
        Assert.Equal(ResistancePhase.VoteResult, game.Phase);
        Assert.Equal(1, game.RejectedTeams);
        Assert.False(ResistanceRoles.IsApproved(game));
    }

    [Fact]
    public async Task ContinuingARejectedTeam_PassesTheLeadershipAndClearsTeamAndVotes()
    {
        var ct = CancellationToken.None;
        var before = await StartGameWithTeamAsync(5);
        await VoteAsync(false, false, false, false, false);

        var result = await _repository.ContinueAsync(_sessionId, null, isManager: true, ct);

        Assert.Equal(ResistanceContinueStatus.Continued, result.Status);
        var game = await ReloadAsync();
        var expected = _players[(_players.IndexOf(before.LeaderPlayerId!.Value) + 1) % 5];
        Assert.Equal(expected, game.LeaderPlayerId);
        Assert.Equal(ResistancePhase.SelectingTeam, game.Phase);
        Assert.Equal(1, game.RejectedTeams);
        Assert.All(game.Players, x =>
        {
            Assert.False(x.IsOnTeam);
            Assert.Null(x.Vote);
        });
    }

    [Fact]
    public async Task Continuing_NeedsTheLeaderOrAManagerAndARejectedTeam()
    {
        var ct = CancellationToken.None;
        var game = await StartGameWithTeamAsync(5);
        var notLeader = _players.First(x => x != game.LeaderPlayerId);

        Assert.Equal(ResistanceContinueStatus.NothingToContinue, (await _repository.ContinueAsync(_sessionId, game.LeaderPlayerId, false, ct)).Status);
        await VoteAsync(false, false, false, false, false);
        Assert.Equal(ResistanceContinueStatus.NotAllowed, (await _repository.ContinueAsync(_sessionId, notLeader, false, ct)).Status);
        Assert.Equal(ResistanceContinueStatus.Continued, (await _repository.ContinueAsync(_sessionId, game.LeaderPlayerId, false, ct)).Status);
    }

    [Fact]
    public async Task AnApprovedTeam_CannotBeContinuedFrom()
    {
        await StartGameWithTeamAsync(5);
        await VoteAsync(true, true, true, true, true);

        var result = await _repository.ContinueAsync(_sessionId, null, isManager: true, CancellationToken.None);

        Assert.Equal(ResistanceContinueStatus.NothingToContinue, result.Status);
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
                await _repository.ContinueAsync(_sessionId, null, true, ct);
                game = await ReloadAsync();
                var team = _players.Take(2).ToList();
                Assert.Equal(ResistanceTeamStatus.Selected, (await _repository.SelectTeamAsync(_sessionId, game.LeaderPlayerId, team, ct)).Status);
            }
        }

        var over = await ReloadAsync();
        Assert.Equal(ResistancePhase.GameOver, over.Phase);
        Assert.Equal(ResistanceRole.Spy, over.Winner);
        Assert.Equal(5, over.RejectedTeams);
        Assert.All(over.Players, x => Assert.NotNull(x.Vote));
        Assert.Equal(ResistanceVoteStatus.GameOver, (await _repository.VoteAsync(_sessionId, _players[0], true, ct)).Status);
        Assert.Equal(ResistanceContinueStatus.GameOver, (await _repository.ContinueAsync(_sessionId, null, true, ct)).Status);
        Assert.Equal(ResistanceTeamStatus.GameOver, (await _repository.SelectTeamAsync(_sessionId, over.LeaderPlayerId, _players.Take(2).ToList(), ct)).Status);
    }

    [Fact]
    public async Task AnApproval_ResetsTheRejectionCount()
    {
        var ct = CancellationToken.None;
        await StartGameWithTeamAsync(5);
        await VoteAsync(false, false, false, false, false);
        await _repository.ContinueAsync(_sessionId, null, true, ct);
        var game = await ReloadAsync();
        await _repository.SelectTeamAsync(_sessionId, game.LeaderPlayerId, _players.Take(2).ToList(), ct);

        await VoteAsync(true, true, true, true, true);

        Assert.Equal(0, (await ReloadAsync()).RejectedTeams);
    }

    [Fact]
    public async Task ARejectedTeam_CanBeSelectedOnlyAfterContinuing()
    {
        var game = await StartGameWithTeamAsync(5);
        await VoteAsync(false, false, false, false, false);

        var result = await _repository.SelectTeamAsync(
            _sessionId, game.LeaderPlayerId, _players.Take(2).ToList(), CancellationToken.None);

        Assert.Equal(ResistanceTeamStatus.AlreadySubmitted, result.Status);
    }
}
