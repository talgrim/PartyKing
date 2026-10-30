using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Domain.Entities.Games;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Application.Tests;

/// <summary>The plot cards add-on against a real SQLite file: the deck rows, drawing, dealing and the restart.</summary>
public class ResistancePlotCardsRepositoryTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"partyking-tests-{Guid.NewGuid():N}");
    private string ConnectionString => $"Data Source={Path.Combine(_directory, "PartyKing.db")}";
    private ServiceProvider _provider = null!;
    private IDbContextFactory<WriterDbContext> _writer = null!;
    private ResistanceGamesRepository _repository = null!;
    private Guid _sessionId;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);

        _provider = new ServiceCollection()
            .AddDbContextFactory<WriterDbContext>(options => options.UseSqlite(ConnectionString))
            .AddDbContextFactory<ReaderDbContext>(options => options.UseSqlite(ConnectionString))
            .BuildServiceProvider();

        _writer = _provider.GetRequiredService<IDbContextFactory<WriterDbContext>>();
        await using (var dbContext = await _writer.CreateDbContextAsync())
        {
            await dbContext.Database.MigrateAsync();

            var session = new Session("ABC123", "Party", "hash");
            _sessionId = session.Id;
            dbContext.Sessions.Add(session);
            await dbContext.SaveChangesAsync();
        }

        _repository = new ResistanceGamesRepository(
            _provider.GetRequiredService<IDbContextFactory<ReaderDbContext>>(),
            _writer);
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    /// <param name="withoutEstablishConfidence">Swaps Establish Confidence for a third Overheard Conversation, which the
    /// leader would keep instead of handing out, so a test can count on every drawn card being dealt.</param>
    private async Task<ResistanceGame> StartAsync(int playerCount, bool plotCards, bool withoutEstablishConfidence = false)
    {
        var game = new ResistanceGame { SessionId = _sessionId, CreatedAt = DateTimeOffset.UtcNow, IsOpen = true };
        game.Rules.PlotCardsEnabled = plotCards;
        if (withoutEstablishConfidence)
        {
            game.Rules.PlotDeck[ResistancePlotCardType.EstablishConfidence] = 0;
            game.Rules.PlotDeck[ResistancePlotCardType.OverheardConversation] = 3;
        }

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
        var started = await _repository.StartAsync(_sessionId, DateTimeOffset.UtcNow, CancellationToken.None);
        Assert.Equal(ResistanceStartStatus.Started, started.Status);

        return await ReloadAsync();
    }

    private async Task<ResistanceGame> ReloadAsync() => (await _repository.GetAsync(_sessionId, CancellationToken.None))!;

    private static List<PlotCardAssignment> DealEvenly(ResistanceGame game)
    {
        var others = game.Players.Where(x => x.PlayerId != game.LeaderPlayerId).Select(x => x.PlayerId).ToList();

        return ResistanceRoles.CardsToDeal(game)
            .Select((card, i) => new PlotCardAssignment(card.CardId, others[i % others.Count]))
            .ToList();
    }

    private async Task DealAsync(ResistanceGame game)
    {
        var dealt = await _repository.DealPlotCardsAsync(
            _sessionId, game.LeaderPlayerId, DealEvenly(game), CancellationToken.None);
        Assert.Equal(ResistanceDealStatus.Dealt, dealt.Status);
    }

    private async Task SelectTeamAndVoteAsync(ResistanceGame game, bool approve)
    {
        var ct = CancellationToken.None;
        var size = game.Rules.TeamSize(game.Players.Count, game.MissionNumber);
        var team = game.Players.OrderBy(x => x.Order).Take(size).Select(x => x.PlayerId).ToList();
        Assert.Equal(ResistanceTeamStatus.Selected, (await _repository.SelectTeamAsync(_sessionId, game.LeaderPlayerId, team, ct)).Status);
        foreach (var player in game.Players)
        {
            await _repository.VoteAsync(_sessionId, player.PlayerId, approve, ct);
        }
    }

    [Fact]
    public async Task WithTheAddOnOff_NoCardsAreBuiltAndTheLeaderPicksAtOnce()
    {
        var game = await StartAsync(5, plotCards: false);

        Assert.Empty(game.PlotCards);
        Assert.Equal(ResistancePhase.SelectingTeam, game.Phase);
    }

    [Theory]
    [InlineData(5, 1)]
    [InlineData(6, 1)]
    [InlineData(7, 2)]
    [InlineData(8, 2)]
    [InlineData(9, 3)]
    [InlineData(10, 3)]
    public async Task StartingWithTheAddOn_BuildsTheDeckAndTheLeaderDrawsBeforeThePlayerCount(int players, int drawn)
    {
        var game = await StartAsync(players, plotCards: true);

        Assert.Equal(15, game.PlotCards.Count);
        Assert.Equal(ResistancePhase.DealingCards, game.Phase);
        var drawnCards = game.PlotCards.Where(x => x.Location == ResistancePlotCardLocation.Drawn).ToList();
        Assert.Equal(drawn, drawnCards.Count);
        Assert.All(drawnCards, x => Assert.Equal(game.LeaderPlayerId, x.HolderPlayerId));
        Assert.Equal(15 - drawn, game.PlotCards.Count(x => x.Location == ResistancePlotCardLocation.Deck));
        Assert.Equal(
            ResistanceRules.DefaultPlotDeck().OrderBy(x => x.Key).ToList(),
            game.PlotCards.GroupBy(x => x.Type).OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Count()).ToList());
    }

    [Fact]
    public async Task TheTeamIsPickedOnlyAfterTheCardsAreDealt()
    {
        var game = await StartAsync(5, plotCards: true);
        var team = game.Players.Take(2).Select(x => x.PlayerId).ToList();

        var early = await _repository.SelectTeamAsync(_sessionId, game.LeaderPlayerId, team, CancellationToken.None);

        Assert.Equal(ResistanceTeamStatus.CardsNotDealt, early.Status);
    }

    [Fact]
    public async Task DealingTheCards_PutsThemInTheHandsAndMovesOnToPickingTheTeam()
    {
        var game = await StartAsync(7, plotCards: true, withoutEstablishConfidence: true);

        await DealAsync(game);

        var after = await ReloadAsync();
        Assert.Equal(ResistancePhase.SelectingTeam, after.Phase);
        Assert.DoesNotContain(after.PlotCards, x => x.Location == ResistancePlotCardLocation.Drawn);
        var inHands = after.PlotCards.Where(x => x.Location == ResistancePlotCardLocation.Hand).ToList();
        Assert.Equal(2, inHands.Count);
        Assert.DoesNotContain(inHands, x => x.HolderPlayerId == after.LeaderPlayerId);
    }

    [Fact]
    public async Task OnlyTheLeaderDeals_AndOnlyOnce()
    {
        var ct = CancellationToken.None;
        var game = await StartAsync(5, plotCards: true);
        var notLeader = game.Players.First(x => x.PlayerId != game.LeaderPlayerId).PlayerId;

        Assert.Equal(ResistanceDealStatus.NotLeader, (await _repository.DealPlotCardsAsync(_sessionId, notLeader, DealEvenly(game), ct)).Status);
        Assert.Equal(ResistanceDealStatus.NotLeader, (await _repository.DealPlotCardsAsync(_sessionId, null, DealEvenly(game), ct)).Status);
        Assert.Equal(ResistanceDealStatus.NoGame, (await _repository.DealPlotCardsAsync(Guid.NewGuid(), game.LeaderPlayerId, [], ct)).Status);

        await DealAsync(game);

        Assert.Equal(ResistanceDealStatus.NotDealing, (await _repository.DealPlotCardsAsync(_sessionId, game.LeaderPlayerId, [], ct)).Status);
    }

    [Fact]
    public async Task ADealThatIsWrong_ChangesNothing()
    {
        var ct = CancellationToken.None;
        var game = await StartAsync(7, plotCards: true, withoutEstablishConfidence: true);
        var cards = ResistanceRoles.CardsToDeal(game).ToList();
        var other = game.Players.First(x => x.PlayerId != game.LeaderPlayerId).PlayerId;

        var toSelf = await _repository.DealPlotCardsAsync(
            _sessionId, game.LeaderPlayerId, cards.Select(x => new PlotCardAssignment(x.CardId, game.LeaderPlayerId!.Value)).ToList(), ct);
        var toStranger = await _repository.DealPlotCardsAsync(
            _sessionId, game.LeaderPlayerId, cards.Select(x => new PlotCardAssignment(x.CardId, Guid.NewGuid())).ToList(), ct);
        var incomplete = await _repository.DealPlotCardsAsync(
            _sessionId, game.LeaderPlayerId, [new PlotCardAssignment(cards[0].CardId, other)], ct);
        var unknown = await _repository.DealPlotCardsAsync(
            _sessionId, game.LeaderPlayerId, [new PlotCardAssignment(99, other), new PlotCardAssignment(98, other)], ct);
        var twice = await _repository.DealPlotCardsAsync(
            _sessionId, game.LeaderPlayerId, [new PlotCardAssignment(cards[0].CardId, other), new PlotCardAssignment(cards[0].CardId, other)], ct);

        Assert.Equal(ResistanceDealStatus.InvalidReceiver, toSelf.Status);
        Assert.Equal(ResistanceDealStatus.InvalidReceiver, toStranger.Status);
        Assert.Equal(ResistanceDealStatus.Incomplete, incomplete.Status);
        Assert.Equal(ResistanceDealStatus.UnknownCard, unknown.Status);
        Assert.Equal(ResistanceDealStatus.UnknownCard, twice.Status);
        var after = await ReloadAsync();
        Assert.Equal(ResistancePhase.DealingCards, after.Phase);
        Assert.Equal(2, after.PlotCards.Count(x => x.Location == ResistancePlotCardLocation.Drawn));
    }

    [Fact]
    public async Task EstablishConfidence_StaysWithTheLeaderAndCountsAsADrawnCard()
    {
        var ct = CancellationToken.None;
        var game = await StartAsync(7, plotCards: true, withoutEstablishConfidence: true);
        var drawn = game.PlotCards.Where(x => x.Location == ResistancePlotCardLocation.Drawn).OrderBy(x => x.CardId).ToList();
        await using (var dbContext = await _writer.CreateDbContextAsync())
        {
            var card = await dbContext.ResistancePlotCards.SingleAsync(x => x.CardId == drawn[0].CardId);
            card.Type = ResistancePlotCardType.EstablishConfidence;
            await dbContext.SaveChangesAsync();
        }

        game = await ReloadAsync();
        var toDeal = ResistanceRoles.CardsToDeal(game).ToList();
        Assert.Single(toDeal);
        var other = game.Players.First(x => x.PlayerId != game.LeaderPlayerId).PlayerId;

        var keptAway = await _repository.DealPlotCardsAsync(
            _sessionId, game.LeaderPlayerId,
            [new PlotCardAssignment(toDeal[0].CardId, other), new PlotCardAssignment(drawn[0].CardId, other)], ct);
        Assert.Equal(ResistanceDealStatus.UnknownCard, keptAway.Status);

        await DealAsync(game);

        var after = await ReloadAsync();
        var kept = after.PlotCards.Single(x => x.CardId == drawn[0].CardId);
        Assert.Equal(ResistancePlotCardLocation.Hand, kept.Location);
        Assert.Equal(after.LeaderPlayerId, kept.HolderPlayerId);
        Assert.Equal(other, after.PlotCards.Single(x => x.CardId == toDeal[0].CardId).HolderPlayerId);
    }

    [Fact]
    public async Task ARejectedTeam_DoesNotDrawAgain()
    {
        var game = await StartAsync(5, plotCards: true);
        await DealAsync(game);
        game = await ReloadAsync();

        await SelectTeamAndVoteAsync(game, approve: false);

        var after = await ReloadAsync();
        Assert.Equal(ResistancePhase.SelectingTeam, after.Phase);
        Assert.NotEqual(game.LeaderPlayerId, after.LeaderPlayerId);
        Assert.Equal(1, after.PlotCards.Count(x => x.Location == ResistancePlotCardLocation.Hand));
        Assert.Equal(14, after.PlotCards.Count(x => x.Location == ResistancePlotCardLocation.Deck));
    }

    [Fact]
    public async Task TheNextMission_TheNewLeaderDrawsAndTheHandsStay()
    {
        var ct = CancellationToken.None;
        var game = await StartAsync(5, plotCards: true);
        await DealAsync(game);
        game = await ReloadAsync();
        await SelectTeamAndVoteAsync(game, approve: true);
        game = await ReloadAsync();
        foreach (var member in game.Players.Where(x => x.IsOnTeam))
        {
            await _repository.PlayMissionCardAsync(_sessionId, member.PlayerId, true, ct);
        }

        await _repository.ContinueAsync(_sessionId, game.LeaderPlayerId, true, ct);

        var after = await ReloadAsync();
        Assert.Equal(2, after.MissionNumber);
        Assert.Equal(ResistancePhase.DealingCards, after.Phase);
        var drawn = after.PlotCards.Where(x => x.Location == ResistancePlotCardLocation.Drawn).ToList();
        Assert.Single(drawn);
        Assert.Equal(after.LeaderPlayerId, drawn[0].HolderPlayerId);
        Assert.Equal(1, after.PlotCards.Count(x => x.Location == ResistancePlotCardLocation.Hand));
    }

    [Fact]
    public async Task RestartingAFinishedGame_BuildsAFreshDeck()
    {
        var game = await StartAsync(5, plotCards: true);
        for (var i = 0; i < ResistanceRoles.MaxRejectedTeams; i++)
        {
            if (i == 0)
            {
                await DealAsync(game);
            }

            game = await ReloadAsync();
            await SelectTeamAndVoteAsync(game, approve: false);
        }

        Assert.Equal(ResistancePhase.GameOver, (await ReloadAsync()).Phase);

        var restarted = await _repository.RestartAsync(_sessionId, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(ResistanceRestartStatus.Restarted, restarted.Status);
        var after = await ReloadAsync();
        Assert.Equal(15, after.PlotCards.Count);
        Assert.Equal(ResistancePhase.DealingCards, after.Phase);
        Assert.Equal(1, after.PlotCards.Count(x => x.Location == ResistancePlotCardLocation.Drawn));
        Assert.Equal(14, after.PlotCards.Count(x => x.Location == ResistancePlotCardLocation.Deck));
    }
}
