using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Domain.Entities.Games.Resistance;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Infrastructure.Metrics;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Application.Tests;

/// <summary>The plot cards add-on against a real SQLite file: the deck rows, drawing, dealing and the restart.</summary>
public class ResistancePlotCardsRepositoryTests : IAsyncLifetime
{
    private readonly RepositoryMetrics _metrics = new();
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
            _writer, _metrics);
    }

    public async Task DisposeAsync()
    {
        _metrics.Dispose();
        await _provider.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    /// <param name="withoutImmediateCards">Swaps the immediate cards, Strong Leader and No Confidence (whose windows interrupt the round) for Keeping a Close Eye, so a test can count on every drawn
    /// card being dealt into a hand and the team being picked right after.</param>
    /// <param name="onlyType">Makes the whole deck of this type.</param>
    private async Task<ResistanceGame> StartAsync(
        int playerCount, bool plotCards, bool withoutImmediateCards = false, bool onlyOpinionMakers = false,
        ResistancePlotCardType? onlyType = null)
    {
        var game = new ResistanceGame { SessionId = _sessionId, CreatedAt = DateTimeOffset.UtcNow, IsOpen = true };
        game.Rules.PlotCardsEnabled = plotCards;
        if (withoutImmediateCards)
        {
            game.Rules.PlotDeck[ResistancePlotCardType.OverheardConversation] = 0;
            game.Rules.PlotDeck[ResistancePlotCardType.EstablishConfidence] = 0;
            game.Rules.PlotDeck[ResistancePlotCardType.OpenUp] = 0;
            game.Rules.PlotDeck[ResistancePlotCardType.StrongLeader] = 0;
            game.Rules.PlotDeck[ResistancePlotCardType.NoConfidence] = 0;
            game.Rules.PlotDeck[ResistancePlotCardType.KeepingCloseEye] = 11;
        }

        if (onlyType is { } type)
        {
            game.Rules.PlotDeck.Clear();
            game.Rules.PlotDeck[type] = 15;
        }

        if (onlyOpinionMakers)
        {
            game.Rules.PlotDeck.Clear();
            game.Rules.PlotDeck[ResistancePlotCardType.OpinionMaker] = 15;
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
        var started = await StartAsync();
        Assert.Null(started.Failure);

        return await ReloadAsync();
    }

    /// <summary>In a hand or face up in front of a player (an Opinion Maker): dealt out either way.</summary>
    private static bool IsHeld(ResistancePlotCard card) =>
        card.Location is ResistancePlotCardLocation.Hand or ResistancePlotCardLocation.InPlay;

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

    private Task<ResistanceOutcome> UpdateAsync(string operation, Func<ResistanceGame, ResistanceOutcome> action)
    {
        return _repository.UpdateAsync(_sessionId, operation, ResistanceOutcome.NoGame, action, CancellationToken.None);
    }

    private Task<ResistanceOutcome> DealPlotCardsAsync(Guid? caller, IReadOnlyList<PlotCardAssignment> assignments) =>
        UpdateAsync("DealPlotCardsAsync", game => game.DealPlotCards(caller, assignments));

    private Task<ResistanceOutcome> PlayPlotCardAsync(Guid? caller, int cardId, Guid target) =>
        UpdateAsync("PlayPlotCardAsync", game => game.PlayPlotCard(caller, cardId, target, DateTimeOffset.UtcNow));

    private Task<ResistanceOutcome> ConfirmPlotCardAsync(Guid? caller, int cardId) =>
        UpdateAsync("ConfirmPlotCardAsync", game => game.ConfirmPlotCard(caller, cardId));

    private Task<ResistanceOutcome> TakeResponsibilityAsync(
        Guid? caller, int cardId, Guid target, bool? takeOpinionMaker) =>
        UpdateAsync(
            "TakeResponsibilityAsync",
            game => game.TakeResponsibility(
                caller, cardId, target, takeOpinionMaker, DateTimeOffset.UtcNow, Random.Shared));

    private Task<ResistanceOutcome> TakeLeadAsync(Guid? caller, int cardId) =>
        UpdateAsync("TakeLeadAsync", game => game.TakeLead(caller, cardId, DateTimeOffset.UtcNow));

    private Task<ResistanceOutcome> PeekMissionCardAsync(Guid? caller, int cardId, Guid target) =>
        UpdateAsync("PeekMissionCardAsync", game => game.PeekMissionCard(caller, cardId, target, DateTimeOffset.UtcNow));

    private Task<ResistanceOutcome> PlayNoConfidenceAsync(Guid? caller, int cardId) =>
        UpdateAsync("PlayNoConfidenceAsync", game => game.PlayNoConfidence(caller, cardId, DateTimeOffset.UtcNow));

    private Task<ResistanceOutcome> PassLeaderWindowAsync(Guid? caller) =>
        UpdateAsync("PassLeaderWindowAsync", game => game.PassLeaderWindow(caller));

    private async Task<ResistanceGame> ReloadAsync() => (await _repository.GetAsync(_sessionId, CancellationToken.None))!;

    private static List<PlotCardAssignment> DealEvenly(ResistanceGame game)
    {
        var others = game.Players.Where(x => x.PlayerId != game.LeaderPlayerId).Select(x => x.PlayerId).ToList();

        return ResistanceRoles.CardsToDeal(game)
            .Select((card, i) => new PlotCardAssignment(card.CardId, others[i % others.Count]))
            .ToList();
    }

    /// <summary>Hands the drawn cards out and, when that leaves immediate cards to play, plays and confirms them all.</summary>
    private async Task DealAsync(ResistanceGame game)
    {
        await DealOnlyAsync(game);

        var ct = CancellationToken.None;
        var current = await ReloadAsync();
        foreach (var card in ResistanceRoles.UnplayedImmediateCards(current).ToList())
        {
            var target = ResistanceTarget(current, card);
            Assert.Null((await PlayPlotCardAsync(card.HolderPlayerId, card.CardId, target)).Failure);
        }

        current = await ReloadAsync();
        foreach (var card in ResistanceRoles.UnconfirmedCards(current).ToList())
        {
            Assert.Null((await ConfirmPlotCardAsync(ResistanceRoles.Viewer(card), card.CardId)).Failure);
        }
    }

    /// <summary>Hands the drawn cards out and leaves the immediate ones unplayed.</summary>
    private async Task DealOnlyAsync(ResistanceGame game)
    {
        var dealt = await DealPlotCardsAsync(game.LeaderPlayerId, DealEvenly(game));
        Assert.Null(dealt.Failure);
    }

    /// <summary>A legal target: the holder's left neighbour for Overheard Conversation, else the next player after them.</summary>
    private static Guid ResistanceTarget(ResistanceGame game, ResistancePlotCard card) =>
        ResistanceRoles.Neighbours(game, card.HolderPlayerId!.Value)[0];

    /// <summary>Everybody confirms in the open windows one after another (up to <paramref name="stopAt"/>); nothing happens when none is open.</summary>
    private async Task PassWindowsAsync(ResistancePhase? stopAt = null)
    {
        while (true)
        {
            var game = await ReloadAsync();
            if (game.Phase is not (ResistancePhase.LeaderWindow or ResistancePhase.ConfidenceWindow
                    or ResistancePhase.PeekWindow)
                || game.Phase == stopAt)
            {
                return;
            }

            foreach (var player in game.Players)
            {
                await PassLeaderWindowAsync(player.PlayerId);
            }
        }
    }

    /// <param name="passWindows">Confirms the window a vote may open (any card in a hand opens one), so the round goes on.</param>
    private async Task SelectTeamAndVoteAsync(ResistanceGame game, bool approve, bool passWindows = true)
    {
        var ct = CancellationToken.None;
        var size = game.Rules.TeamSize(game.Players.Count, game.MissionNumber);
        var team = game.Players.OrderBy(x => x.Order).Take(size).Select(x => x.PlayerId).ToList();
        Assert.Null((await SelectTeamAsync(game.LeaderPlayerId, team)).Failure);
        var makers = ResistanceRoles.OpinionMakerIds(game);
        foreach (var player in game.Players.OrderBy(x => makers.Contains(x.PlayerId) ? 0 : 1))
        {
            await CastVoteAsync(player.PlayerId, approve);
        }

        if (passWindows)
        {
            await PassWindowsAsync();
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

        var early = await SelectTeamAsync(game.LeaderPlayerId, team);

        Assert.Equal(ResistanceFailure.CardsNotDealt, early.Failure);
    }

    [Fact]
    public async Task DealingTheCards_PutsThemInTheHandsAndMovesOnToPickingTheTeam()
    {
        var game = await StartAsync(7, plotCards: true, withoutImmediateCards: true);

        await DealAsync(game);

        var after = await ReloadAsync();
        Assert.Equal(ResistancePhase.SelectingTeam, after.Phase);
        Assert.DoesNotContain(after.PlotCards, x => x.Location == ResistancePlotCardLocation.Drawn);
        var inHands = after.PlotCards.Where(IsHeld).ToList();
        Assert.Equal(2, inHands.Count);
        Assert.DoesNotContain(inHands, x => x.HolderPlayerId == after.LeaderPlayerId);
    }

    [Fact]
    public async Task OnlyTheLeaderDeals_AndOnlyOnce()
    {
        var ct = CancellationToken.None;
        var game = await StartAsync(5, plotCards: true);
        var notLeader = game.Players.First(x => x.PlayerId != game.LeaderPlayerId).PlayerId;

        Assert.Equal(ResistanceFailure.NotLeaderToDeal, (await DealPlotCardsAsync(notLeader, DealEvenly(game))).Failure);
        Assert.Equal(ResistanceFailure.NotLeaderToDeal, (await DealPlotCardsAsync(null, DealEvenly(game))).Failure);
        Assert.Equal(ResistanceFailure.NoGame, (await _repository.UpdateAsync(
            Guid.NewGuid(), "DealPlotCardsAsync", ResistanceOutcome.NoGame, g => g.DealPlotCards(game.LeaderPlayerId, []), ct)).Failure);

        await DealAsync(game);

        Assert.Equal(ResistanceFailure.NotDealing, (await DealPlotCardsAsync(game.LeaderPlayerId, [])).Failure);
    }

    [Fact]
    public async Task ADealThatIsWrong_ChangesNothing()
    {
        var ct = CancellationToken.None;
        var game = await StartAsync(7, plotCards: true, withoutImmediateCards: true);
        var cards = ResistanceRoles.CardsToDeal(game).ToList();
        var other = game.Players.First(x => x.PlayerId != game.LeaderPlayerId).PlayerId;

        var toSelf = await DealPlotCardsAsync(game.LeaderPlayerId, cards.Select(x => new PlotCardAssignment(x.CardId, game.LeaderPlayerId!.Value)).ToList());
        var toStranger = await DealPlotCardsAsync(game.LeaderPlayerId, cards.Select(x => new PlotCardAssignment(x.CardId, Guid.NewGuid())).ToList());
        var incomplete = await DealPlotCardsAsync(game.LeaderPlayerId, [new PlotCardAssignment(cards[0].CardId, other)]);
        var unknown = await DealPlotCardsAsync(game.LeaderPlayerId, [new PlotCardAssignment(99, other), new PlotCardAssignment(98, other)]);
        var twice = await DealPlotCardsAsync(game.LeaderPlayerId, [new PlotCardAssignment(cards[0].CardId, other), new PlotCardAssignment(cards[0].CardId, other)]);

        Assert.Equal(ResistanceFailure.InvalidReceiver, toSelf.Failure);
        Assert.Equal(ResistanceFailure.InvalidReceiver, toStranger.Failure);
        Assert.Equal(ResistanceFailure.IncompleteDeal, incomplete.Failure);
        Assert.Equal(ResistanceFailure.UnknownDealCard, unknown.Failure);
        Assert.Equal(ResistanceFailure.UnknownDealCard, twice.Failure);
        var after = await ReloadAsync();
        Assert.Equal(ResistancePhase.DealingCards, after.Phase);
        Assert.Equal(2, after.PlotCards.Count(x => x.Location == ResistancePlotCardLocation.Drawn));
    }

    [Fact]
    public async Task EstablishConfidence_StaysWithTheLeaderAndCountsAsADrawnCard()
    {
        var ct = CancellationToken.None;
        var game = await StartAsync(7, plotCards: true, withoutImmediateCards: true);
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

        var keptAway = await DealPlotCardsAsync(game.LeaderPlayerId,
            [new PlotCardAssignment(toDeal[0].CardId, other), new PlotCardAssignment(drawn[0].CardId, other)]);
        Assert.Equal(ResistanceFailure.UnknownDealCard, keptAway.Failure);

        await DealOnlyAsync(game);

        var after = await ReloadAsync();
        var kept = after.PlotCards.Single(x => x.CardId == drawn[0].CardId);
        Assert.Equal(ResistancePlotCardLocation.Hand, kept.Location);
        Assert.Equal(after.LeaderPlayerId, kept.HolderPlayerId);
        Assert.Equal(other, after.PlotCards.Single(x => x.CardId == toDeal[0].CardId).HolderPlayerId);
    }

    [Fact]
    public async Task ARejectedTeam_DoesNotDrawAgain()
    {
        var game = await StartAsync(5, plotCards: true, withoutImmediateCards: true);
        await DealAsync(game);
        game = await ReloadAsync();

        await SelectTeamAndVoteAsync(game, approve: false);

        var after = await ReloadAsync();
        Assert.Equal(ResistancePhase.SelectingTeam, after.Phase);
        Assert.NotEqual(game.LeaderPlayerId, after.LeaderPlayerId);
        Assert.Equal(1, after.PlotCards.Count(IsHeld));
        Assert.Equal(14, after.PlotCards.Count(x => x.Location == ResistancePlotCardLocation.Deck));
    }

    [Fact]
    public async Task TheNextMission_TheNewLeaderDrawsAndTheHandsStay()
    {
        var ct = CancellationToken.None;
        var game = await StartAsync(5, plotCards: true, withoutImmediateCards: true);
        await DealAsync(game);
        game = await ReloadAsync();
        await SelectTeamAndVoteAsync(game, approve: true);
        game = await ReloadAsync();
        foreach (var member in game.Players.Where(x => x.IsOnTeam))
        {
            await PlayMissionCardAsync(member.PlayerId, true);
        }

        await PassWindowsAsync();
        await ContinueAsync(game.LeaderPlayerId, true);
        await PassWindowsAsync();

        var after = await ReloadAsync();
        Assert.Equal(2, after.MissionNumber);
        Assert.Equal(ResistancePhase.DealingCards, after.Phase);
        var drawn = after.PlotCards.Where(x => x.Location == ResistancePlotCardLocation.Drawn).ToList();
        Assert.Single(drawn);
        Assert.Equal(after.LeaderPlayerId, drawn[0].HolderPlayerId);
        Assert.Equal(1, after.PlotCards.Count(IsHeld));
    }

    [Fact]
    public async Task RestartingAFinishedGame_BuildsAFreshDeck()
    {
        var game = await StartAsync(5, plotCards: true, withoutImmediateCards: true);
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

        var restarted = await RestartAsync();

        Assert.Null(restarted.Failure);
        Assert.Empty((await ReloadAsync()).PlotCards);

        await StartAsync();
        var after = await ReloadAsync();
        Assert.Equal(15, after.PlotCards.Count);
        Assert.Equal(ResistancePhase.DealingCards, after.Phase);
        Assert.Equal(1, after.PlotCards.Count(x => x.Location == ResistancePlotCardLocation.Drawn));
        Assert.Equal(14, after.PlotCards.Count(x => x.Location == ResistancePlotCardLocation.Deck));
    }

    [Fact]
    public async Task AnOpinionMaker_IsPlayedWhenDealt_AndIsNotCountedInTheHand()
    {
        var game = await StartAsync(5, plotCards: true, onlyOpinionMakers: true);
        await DealAsync(game);

        game = await ReloadAsync();
        var card = Assert.Single(game.PlotCards, x => x.Location == ResistancePlotCardLocation.InPlay);
        Assert.Equal(ResistancePlotCardType.OpinionMaker, card.Type);
        Assert.Equal(card.HolderPlayerId, Assert.Single(ResistanceRoles.OpinionMakerIds(game)));
        Assert.DoesNotContain(game.PlotCards, x => x.Location == ResistancePlotCardLocation.Hand);
    }

    [Fact]
    public async Task WhileAnOpinionMakerHasNotVoted_TheOthersCannotVote_AndTheirVoteIsLocked()
    {
        var ct = CancellationToken.None;
        var game = await StartAsync(5, plotCards: true, onlyOpinionMakers: true);
        await DealAsync(game);
        game = await ReloadAsync();
        var maker = ResistanceRoles.OpinionMakerIds(game).Single();
        var other = game.Players.First(x => x.PlayerId != maker).PlayerId;
        var size = game.Rules.TeamSize(game.Players.Count, game.MissionNumber);
        var team = game.Players.OrderBy(x => x.Order).Take(size).Select(x => x.PlayerId).ToList();
        await SelectTeamAsync(game.LeaderPlayerId, team);

        Assert.Equal(ResistanceFailure.OpinionMakersFirst, (await CastVoteAsync(other, true)).Failure);
        Assert.Null((await CastVoteAsync(maker, false)).Failure);
        Assert.Equal(ResistanceFailure.VoteLocked, (await CastVoteAsync(maker, true)).Failure);

        Assert.Null((await CastVoteAsync(other, true)).Failure);
        // Players other than the maker can still change their mind
        Assert.Null((await CastVoteAsync(other, false)).Failure);
        Assert.False((await ReloadAsync()).Players.Single(x => x.PlayerId == maker).Vote);
    }

    [Fact]
    public async Task TwoOpinionMakers_BothMustVoteBeforeTheOthersMay()
    {
        var ct = CancellationToken.None;
        var game = await StartAsync(7, plotCards: true, onlyOpinionMakers: true);
        await DealAsync(game);
        game = await ReloadAsync();
        var makers = ResistanceRoles.OpinionMakerIds(game);
        Assert.Equal(2, makers.Count);
        var other = game.Players.First(x => !makers.Contains(x.PlayerId)).PlayerId;
        var size = game.Rules.TeamSize(game.Players.Count, game.MissionNumber);
        var team = game.Players.OrderBy(x => x.Order).Take(size).Select(x => x.PlayerId).ToList();
        await SelectTeamAsync(game.LeaderPlayerId, team);

        await CastVoteAsync(makers[0], true);
        Assert.Equal(ResistanceFailure.OpinionMakersFirst, (await CastVoteAsync(other, true)).Failure);

        await CastVoteAsync(makers[1], true);
        Assert.Null((await CastVoteAsync(other, true)).Failure);
    }

    [Fact]
    public async Task RestartingAFinishedGame_TakesTheOpinionMakersOutOfPlay()
    {
        var ct = CancellationToken.None;
        var game = await StartAsync(5, plotCards: true, onlyOpinionMakers: true);
        await DealAsync(game);
        game = await ReloadAsync();
        for (var i = 0; i < 5 && game.Phase != ResistancePhase.GameOver; i++)
        {
            await SelectTeamAndVoteAsync(game, approve: false);
            game = await ReloadAsync();
            if (game.Phase == ResistancePhase.DealingCards)
            {
                await DealAsync(game);
                game = await ReloadAsync();
            }
        }

        Assert.Equal(ResistancePhase.GameOver, game.Phase);
        Assert.Null((await RestartAsync()).Failure);
        Assert.Empty(ResistanceRoles.OpinionMakerIds(await ReloadAsync()));
    }

    [Fact]
    public async Task DealingAnImmediateCard_StartsPlayingItBeforeTheTeamIsPicked()
    {
        var game = await StartAsync(5, plotCards: true, onlyType: ResistancePlotCardType.OpenUp);

        await DealOnlyAsync(game);

        var after = await ReloadAsync();
        Assert.Equal(ResistancePhase.ResolvingCards, after.Phase);
        Assert.Equal(ResistanceFailure.CardsNotDealt, (await SelectTeamAsync(after.LeaderPlayerId, after.Players.Take(2).Select(x => x.PlayerId).ToList())).Failure);
    }

    [Fact]
    public async Task OverheardConversation_IsPlayedOnANeighbourAndTheHolderConfirmsWhatTheySaw()
    {
        var ct = CancellationToken.None;
        var game = await StartAsync(5, plotCards: true, onlyType: ResistancePlotCardType.OverheardConversation);
        await DealOnlyAsync(game);
        var card = ResistanceRoles.UnplayedImmediateCards(await ReloadAsync()).Single();
        var holder = card.HolderPlayerId!.Value;
        var neighbours = ResistanceRoles.Neighbours(game, holder);
        var farAway = game.Players.Select(x => x.PlayerId).First(x => x != holder && !neighbours.Contains(x));

        Assert.Equal(ResistanceFailure.InvalidTarget, (await PlayPlotCardAsync(holder, card.CardId, farAway)).Failure);
        Assert.Equal(ResistanceFailure.InvalidTarget, (await PlayPlotCardAsync(holder, card.CardId, holder)).Failure);
        Assert.Equal(ResistanceFailure.NotYourCard, (await PlayPlotCardAsync(farAway, card.CardId, neighbours[0])).Failure);
        Assert.Equal(ResistanceFailure.CardState, (await ConfirmPlotCardAsync(holder, card.CardId)).Failure);

        Assert.Null((await PlayPlotCardAsync(holder, card.CardId, neighbours[1])).Failure);

        var played = await ReloadAsync();
        var log = played.PlotCards.Single(x => x.CardId == card.CardId);
        Assert.Equal(ResistancePlotCardLocation.Played, log.Location);
        Assert.Equal(neighbours[1], log.TargetPlayerId);
        Assert.Equal(1, log.PlayedInMission);
        Assert.Equal(ResistancePhase.ResolvingCards, played.Phase);
        Assert.Equal(ResistanceFailure.CardState, (await PlayPlotCardAsync(holder, card.CardId, neighbours[0])).Failure);
        // The holder sees the neighbour's identity card, so nobody else may confirm it
        Assert.Equal(ResistanceFailure.NotYourCard, (await ConfirmPlotCardAsync(neighbours[1], card.CardId)).Failure);

        Assert.Null((await ConfirmPlotCardAsync(holder, card.CardId)).Failure);
        Assert.Equal(ResistancePhase.SelectingTeam, (await ReloadAsync()).Phase);
        Assert.Equal(ResistanceFailure.NotResolving, (await ConfirmPlotCardAsync(holder, card.CardId)).Failure);
    }

    [Fact]
    public async Task OpenUp_ShowsTheHoldersIdentityToTheChosenPlayerWhoConfirmsIt()
    {
        var ct = CancellationToken.None;
        var game = await StartAsync(5, plotCards: true, onlyType: ResistancePlotCardType.OpenUp);
        await DealOnlyAsync(game);
        var card = ResistanceRoles.UnplayedImmediateCards(await ReloadAsync()).Single();
        var holder = card.HolderPlayerId!.Value;
        var chosen = game.Players.Select(x => x.PlayerId).First(x => x != holder && x != game.LeaderPlayerId);

        Assert.Equal(ResistanceFailure.InvalidTarget, (await PlayPlotCardAsync(holder, card.CardId, holder)).Failure);
        Assert.Equal(ResistanceFailure.InvalidTarget, (await PlayPlotCardAsync(holder, card.CardId, Guid.NewGuid())).Failure);
        Assert.Null((await PlayPlotCardAsync(holder, card.CardId, chosen)).Failure);

        var played = (await ReloadAsync()).PlotCards.Single(x => x.CardId == card.CardId);
        Assert.Equal(chosen, ResistanceRoles.Viewer(played));
        Assert.Equal(holder, ResistanceRoles.Revealed(played));
        Assert.Equal(ResistanceFailure.NotYourCard, (await ConfirmPlotCardAsync(holder, card.CardId)).Failure);
        Assert.Null((await ConfirmPlotCardAsync(chosen, card.CardId)).Failure);
        Assert.Equal(ResistancePhase.SelectingTeam, (await ReloadAsync()).Phase);
    }

    [Fact]
    public async Task OpenUp_MayBeShownToTheLeader()
    {
        var game = await StartAsync(5, plotCards: true, onlyType: ResistancePlotCardType.OpenUp);
        await DealOnlyAsync(game);
        var card = ResistanceRoles.UnplayedImmediateCards(await ReloadAsync()).Single();

        var played = await PlayPlotCardAsync(card.HolderPlayerId, card.CardId, game.LeaderPlayerId!.Value);

        Assert.Null(played.Failure);
    }

    [Fact]
    public async Task EstablishConfidence_StaysWithTheLeaderWhoPlaysItOnAnotherPlayer()
    {
        var ct = CancellationToken.None;
        var game = await StartAsync(5, plotCards: true, onlyType: ResistancePlotCardType.EstablishConfidence);
        // The only drawn card is the leader's own, so there is nothing to hand out
        Assert.Empty(ResistanceRoles.CardsToDeal(game));
        await DealOnlyAsync(game);
        var leader = game.LeaderPlayerId!.Value;
        var card = ResistanceRoles.UnplayedImmediateCards(await ReloadAsync()).Single();
        var chosen = game.Players.Select(x => x.PlayerId).First(x => x != leader);

        Assert.Equal(leader, card.HolderPlayerId);
        Assert.Equal(ResistanceFailure.NotYourCard, (await PlayPlotCardAsync(chosen, card.CardId, leader)).Failure);
        Assert.Equal(ResistanceFailure.InvalidTarget, (await PlayPlotCardAsync(leader, card.CardId, leader)).Failure);
        Assert.Null((await PlayPlotCardAsync(leader, card.CardId, chosen)).Failure);
        Assert.Equal(ResistanceFailure.NotYourCard, (await ConfirmPlotCardAsync(leader, card.CardId)).Failure);
        Assert.Null((await ConfirmPlotCardAsync(chosen, card.CardId)).Failure);
        Assert.Equal(ResistancePhase.SelectingTeam, (await ReloadAsync()).Phase);
    }

    [Fact]
    public async Task SeveralImmediateCards_ArePlayedInAnyOrderAndTheTeamWaitsForTheLastConfirmation()
    {
        var ct = CancellationToken.None;
        var game = await StartAsync(7, plotCards: true, onlyType: ResistancePlotCardType.OpenUp);
        await DealOnlyAsync(game);
        var cards = ResistanceRoles.UnplayedImmediateCards(await ReloadAsync()).OrderBy(x => x.CardId).ToList();
        Assert.Equal(2, cards.Count);
        var targets = cards.Select(x => game.Players.Select(p => p.PlayerId).First(p => p != x.HolderPlayerId)).ToList();

        // The second card is played and confirmed before the first one is played at all
        await PlayPlotCardAsync(cards[1].HolderPlayerId, cards[1].CardId, targets[1]);
        await ConfirmPlotCardAsync(targets[1], cards[1].CardId);
        Assert.Equal(ResistancePhase.ResolvingCards, (await ReloadAsync()).Phase);

        await PlayPlotCardAsync(cards[0].HolderPlayerId, cards[0].CardId, targets[0]);
        Assert.Equal(ResistancePhase.ResolvingCards, (await ReloadAsync()).Phase);

        await ConfirmPlotCardAsync(targets[0], cards[0].CardId);
        Assert.Equal(ResistancePhase.SelectingTeam, (await ReloadAsync()).Phase);
    }

    [Fact]
    public async Task PlayingOrConfirming_IsRefusedOutsideThePlayingPhase()
    {
        var ct = CancellationToken.None;
        var game = await StartAsync(5, plotCards: true, onlyType: ResistancePlotCardType.OpenUp);
        var someone = game.Players.First(x => x.PlayerId != game.LeaderPlayerId).PlayerId;

        // A non-holder learns nothing about the card, not even through the phase check
        Assert.Equal(ResistanceFailure.NotYourCard, (await PlayPlotCardAsync(someone, 0, game.LeaderPlayerId!.Value)).Failure);
        Assert.Equal(ResistanceFailure.NoGame, (await _repository.UpdateAsync(
            Guid.NewGuid(), "ConfirmPlotCardAsync", ResistanceOutcome.NoGame, g => g.ConfirmPlotCard(someone, 0), ct)).Failure);

        await DealOnlyAsync(game);
        Assert.Equal(ResistanceFailure.NotYourCard, (await PlayPlotCardAsync(someone, 99, game.LeaderPlayerId!.Value)).Failure);
    }

    [Fact]
    public async Task RestartingAFinishedGame_ForgetsThePlayedCards()
    {
        var ct = CancellationToken.None;
        var game = await StartAsync(5, plotCards: true, onlyType: ResistancePlotCardType.OpenUp);
        for (var i = 0; i < 5 && game.Phase != ResistancePhase.GameOver; i++)
        {
            if (game.Phase == ResistancePhase.DealingCards)
            {
                await DealAsync(game);
            }

            await SelectTeamAndVoteAsync(await ReloadAsync(), approve: false);
            game = await ReloadAsync();
        }

        Assert.Equal(ResistancePhase.GameOver, game.Phase);
        Assert.Contains(game.PlotCards, x => x.Location == ResistancePlotCardLocation.Played);
        Assert.Null((await RestartAsync()).Failure);
        var restarted = await ReloadAsync();
        Assert.DoesNotContain(restarted.PlotCards, x => x.Location == ResistancePlotCardLocation.Played || x.IsConfirmed || x.TargetPlayerId is not null);
    }

    /// <summary>A started 5-player game whose team was approved and waits in the confidence window, with the one drawn In the Spotlight dealt.</summary>
    private async Task<(ResistanceGame Game, ResistancePlotCard Card)> PlayingWithSpotlightAsync()
    {
        var game = await StartAsync(5, plotCards: true, onlyType: ResistancePlotCardType.InTheSpotlight);
        await DealAsync(game);
        await SelectTeamAndVoteAsync(await ReloadAsync(), approve: true, passWindows: false);
        game = await ReloadAsync();
        Assert.Equal(ResistancePhase.ConfidenceWindow, game.Phase);

        return (game, game.PlotCards.Single(x => x.Location == ResistancePlotCardLocation.Hand));
    }

    [Fact]
    public async Task InTheSpotlight_ForcesATeamMemberToPlayOpenlyWithoutAskingForAConfirmation()
    {
        var ct = CancellationToken.None;
        var (game, card) = await PlayingWithSpotlightAsync();
        var target = game.Players.First(x => x.IsOnTeam && x.PlayerId != card.HolderPlayerId);

        var played = await PlayPlotCardAsync(card.HolderPlayerId, card.CardId, target.PlayerId);

        Assert.Null(played.Failure);
        var after = await ReloadAsync();
        Assert.Equal(target.PlayerId, ResistanceRoles.OpenMissionPlayerId(after));
        Assert.Empty(ResistanceRoles.UnconfirmedCards(after));
        Assert.Equal(ResistancePhase.ConfidenceWindow, after.Phase);
    }

    [Fact]
    public async Task ConfidenceWindow_BlocksTheMissionCardsUntilEveryoneConfirmed()
    {
        var ct = CancellationToken.None;
        var (game, _) = await PlayingWithSpotlightAsync();
        var member = game.Players.First(x => x.IsOnTeam);

        Assert.Equal(ResistanceFailure.NoApprovedTeam, (await PlayMissionCardAsync(member.PlayerId, true)).Failure);
        foreach (var player in game.Players.Take(4))
        {
            await PassLeaderWindowAsync(player.PlayerId);
        }

        Assert.Equal(ResistancePhase.ConfidenceWindow, (await ReloadAsync()).Phase);
        await PassLeaderWindowAsync(game.Players[4].PlayerId);

        Assert.Equal(ResistancePhase.PlayingMission, (await ReloadAsync()).Phase);
        Assert.Null((await PlayMissionCardAsync(member.PlayerId, true)).Failure);
    }

    [Fact]
    public async Task NoConfidence_GivesBackAnInTheSpotlightPlayedInTheSameWindow()
    {
        var ct = CancellationToken.None;
        var (game, spotlight) = await PlayingWithSpotlightAsync();
        var target = game.Players.First(x => x.IsOnTeam && x.PlayerId != spotlight.HolderPlayerId).PlayerId;
        await PlayPlotCardAsync(spotlight.HolderPlayerId, spotlight.CardId, target);
        var other = game.Players.First(x => x.PlayerId != spotlight.HolderPlayerId).PlayerId;
        await SetCardAsync(SpareCardId(game), other, ResistancePlotCardLocation.Hand, ResistancePlotCardType.NoConfidence);

        var played = await PlayNoConfidenceAsync(other, SpareCardId(game));

        Assert.Null(played.Failure);
        var after = await ReloadAsync();
        var back = after.PlotCards.Single(x => x.CardId == spotlight.CardId);
        Assert.Equal((ResistancePlotCardLocation.Hand, spotlight.HolderPlayerId), (back.Location, back.HolderPlayerId));
        Assert.Null(ResistanceRoles.OpenMissionPlayerId(after));
    }

    [Fact]
    public async Task InTheSpotlight_CannotBePlayedOnceTheMissionCardsAreBeingPlayed()
    {
        var ct = CancellationToken.None;
        var (game, card) = await PlayingWithSpotlightAsync();
        var target = game.Players.First(x => x.IsOnTeam && x.PlayerId != card.HolderPlayerId).PlayerId;
        await PassWindowsAsync();

        var late = await PlayPlotCardAsync(card.HolderPlayerId, card.CardId, target);

        Assert.Equal(ResistanceFailure.NotPlayingMission, late.Failure);
    }

    [Fact]
    public async Task InTheSpotlight_IsPlayedOnlyInTheConfidenceWindow()
    {
        var ct = CancellationToken.None;
        var game = await StartAsync(5, plotCards: true, onlyType: ResistancePlotCardType.InTheSpotlight);
        await DealAsync(game);
        var card = (await ReloadAsync()).PlotCards.Single(x => x.Location == ResistancePlotCardLocation.Hand);
        var target = game.Players.First(x => x.PlayerId != card.HolderPlayerId).PlayerId;

        var early = await PlayPlotCardAsync(card.HolderPlayerId, card.CardId, target);

        Assert.Equal(ResistanceFailure.NotPlayingMission, early.Failure);
    }

    [Fact]
    public async Task InTheSpotlight_RejectsTargetsOutsideTheTeamAndTheHolder()
    {
        var ct = CancellationToken.None;
        var (game, card) = await PlayingWithSpotlightAsync();
        var outsider = game.Players.First(x => !x.IsOnTeam && x.PlayerId != card.HolderPlayerId);

        foreach (var invalid in new[] { outsider.PlayerId, card.HolderPlayerId!.Value, Guid.NewGuid() })
        {
            var result = await PlayPlotCardAsync(card.HolderPlayerId, card.CardId, invalid);
            Assert.Equal(ResistanceFailure.InvalidTarget, result.Failure);
        }

        Assert.Null(ResistanceRoles.OpenMissionPlayerId(await ReloadAsync()));
    }

    [Fact]
    public async Task InTheSpotlight_CanOnlyBePlayedByItsHolderAndOnlyOnce()
    {
        var ct = CancellationToken.None;
        var (game, card) = await PlayingWithSpotlightAsync();
        var target = game.Players.First(x => x.IsOnTeam && x.PlayerId != card.HolderPlayerId).PlayerId;
        var stranger = game.Players.First(x => x.PlayerId != card.HolderPlayerId).PlayerId;

        var byStranger = await PlayPlotCardAsync(stranger, card.CardId, target);
        await PlayPlotCardAsync(card.HolderPlayerId, card.CardId, target);
        var again = await PlayPlotCardAsync(card.HolderPlayerId, card.CardId, target);

        Assert.Equal(ResistanceFailure.NotYourCard, byStranger.Failure);
        Assert.Equal(ResistanceFailure.CardState, again.Failure);
    }

    /// <summary>A game with Take Responsibility dealt to one player; the other deck cards can be moved around with <see cref="SetCardAsync"/>.</summary>
    private async Task<(ResistanceGame Game, ResistancePlotCard Card, Guid Target)> WithTakeResponsibilityAsync()
    {
        var game = await StartAsync(5, plotCards: true, onlyType: ResistancePlotCardType.TakeResponsibility);
        await DealAsync(game);
        game = await ReloadAsync();
        var card = game.PlotCards.Single(x => x.Location == ResistancePlotCardLocation.Hand);
        var target = game.Players.First(x => x.PlayerId != card.HolderPlayerId).PlayerId;
        Assert.Equal(ResistancePhase.SelectingTeam, game.Phase);

        return (game, card, target);
    }

    private async Task SetCardAsync(
        int cardId, Guid holder, ResistancePlotCardLocation location, ResistancePlotCardType? type = null)
    {
        await using var dbContext = await _writer.CreateDbContextAsync();
        var card = await dbContext.Set<ResistancePlotCard>().SingleAsync(x => x.SessionId == _sessionId && x.CardId == cardId);
        (card.HolderPlayerId, card.Location) = (holder, location);
        card.Type = type ?? card.Type;
        await dbContext.SaveChangesAsync();
    }

    private int SpareCardId(ResistanceGame game, int skip = 0) =>
        game.PlotCards.Where(x => x.Location == ResistancePlotCardLocation.Deck).OrderBy(x => x.CardId).Skip(skip).First().CardId;

    [Fact]
    public async Task TakeResponsibility_TakesARandomHandCardFromTheTarget()
    {
        var (game, card, target) = await WithTakeResponsibilityAsync();
        var spare = SpareCardId(game);
        await SetCardAsync(spare, target, ResistancePlotCardLocation.Hand);

        var result = await TakeResponsibilityAsync(card.HolderPlayerId, card.CardId, target, null);

        Assert.Null(result.Failure);
        var after = await ReloadAsync();
        Assert.Equal(card.HolderPlayerId, after.PlotCards.Single(x => x.CardId == spare).HolderPlayerId);
        var played = after.PlotCards.Single(x => x.CardId == card.CardId);
        Assert.Equal((ResistancePlotCardLocation.Played, target, true), (played.Location, played.TargetPlayerId, played.IsConfirmed));
        Assert.Empty(ResistanceRoles.UnconfirmedCards(after));
    }

    [Fact]
    public async Task TakeResponsibility_TakesAFaceUpOpinionMakerWithoutAskingWhenTheHandIsEmpty()
    {
        var (game, card, target) = await WithTakeResponsibilityAsync();
        var spare = SpareCardId(game);
        await SetCardAsync(spare, target, ResistancePlotCardLocation.InPlay, ResistancePlotCardType.OpinionMaker);

        await TakeResponsibilityAsync(card.HolderPlayerId, card.CardId, target, null);

        var after = await ReloadAsync();
        Assert.Equal([card.HolderPlayerId!.Value], ResistanceRoles.OpinionMakerIds(after));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TakeResponsibility_AsksWhichToTakeWhenTheTargetHasBoth(bool takeOpinionMaker)
    {
        var (game, card, target) = await WithTakeResponsibilityAsync();
        var (maker, handCard) = (SpareCardId(game), SpareCardId(game, 1));
        await SetCardAsync(maker, target, ResistancePlotCardLocation.InPlay, ResistancePlotCardType.OpinionMaker);
        await SetCardAsync(handCard, target, ResistancePlotCardLocation.Hand);

        var unanswered = await TakeResponsibilityAsync(card.HolderPlayerId, card.CardId, target, null);
        var answered = await TakeResponsibilityAsync(card.HolderPlayerId, card.CardId, target, takeOpinionMaker);

        Assert.Equal(ResistanceFailure.ChoiceRequired, unanswered.Failure);
        Assert.Null(answered.Failure);
        var after = await ReloadAsync();
        Assert.Equal(takeOpinionMaker ? card.HolderPlayerId : target, after.PlotCards.Single(x => x.CardId == maker).HolderPlayerId);
        Assert.Equal(takeOpinionMaker ? target : card.HolderPlayerId, after.PlotCards.Single(x => x.CardId == handCard).HolderPlayerId);
    }

    [Fact]
    public async Task TakeResponsibility_TakesTheOpinionMakerWhenTheTargetHasNoHandAndTheCallerAsksForIt()
    {
        var (game, card, target) = await WithTakeResponsibilityAsync();
        var maker = SpareCardId(game);
        await SetCardAsync(maker, target, ResistancePlotCardLocation.InPlay, ResistancePlotCardType.OpinionMaker);

        var result = await TakeResponsibilityAsync(card.HolderPlayerId, card.CardId, target, true);

        Assert.Null(result.Failure);
        Assert.Equal(card.HolderPlayerId, (await ReloadAsync()).PlotCards.Single(x => x.CardId == maker).HolderPlayerId);
    }

    [Fact]
    public async Task TakeResponsibility_KeepsAStolenOpinionMakerInPlay_AndTheNewHolderVotesFirstAndIsLocked()
    {
        var ct = CancellationToken.None;
        var (game, card, target) = await WithTakeResponsibilityAsync();
        var (taker, maker) = (card.HolderPlayerId!.Value, SpareCardId(game));
        await SetCardAsync(maker, target, ResistancePlotCardLocation.InPlay, ResistancePlotCardType.OpinionMaker);
        await TakeResponsibilityAsync(taker, card.CardId, target, null);

        var after = await ReloadAsync();
        var stolen = after.PlotCards.Single(x => x.CardId == maker);
        Assert.Equal((ResistancePlotCardLocation.InPlay, taker), (stolen.Location, stolen.HolderPlayerId));
        Assert.Equal([taker], ResistanceRoles.OpinionMakerIds(after));

        var team = after.Players.OrderBy(x => x.Order).Take(after.Rules.TeamSize(after.Players.Count, after.MissionNumber)).Select(x => x.PlayerId).ToList();
        await SelectTeamAsync(after.LeaderPlayerId, team);

        // The previous holder is an ordinary voter now and waits for the new one
        Assert.Equal(ResistanceFailure.OpinionMakersFirst, (await CastVoteAsync(target, true)).Failure);
        Assert.Null((await CastVoteAsync(taker, false)).Failure);
        Assert.Equal(ResistanceFailure.VoteLocked, (await CastVoteAsync(taker, true)).Failure);
        Assert.Null((await CastVoteAsync(target, true)).Failure);
        Assert.Null((await CastVoteAsync(target, false)).Failure);
    }

    [Fact]
    public async Task TakeResponsibility_CountsATakerWhoAlreadyHasAnOpinionMakerOnce()
    {
        var (game, card, target) = await WithTakeResponsibilityAsync();
        var taker = card.HolderPlayerId!.Value;
        var (own, stolen) = (SpareCardId(game), SpareCardId(game, 1));
        await SetCardAsync(own, taker, ResistancePlotCardLocation.InPlay, ResistancePlotCardType.OpinionMaker);
        await SetCardAsync(stolen, target, ResistancePlotCardLocation.InPlay, ResistancePlotCardType.OpinionMaker);

        await TakeResponsibilityAsync(taker, card.CardId, target, null);

        var after = await ReloadAsync();
        Assert.Equal([taker], ResistanceRoles.OpinionMakerIds(after));
        Assert.All(after.PlotCards.Where(x => x.Type == ResistancePlotCardType.OpinionMaker && x.Location == ResistancePlotCardLocation.InPlay),
            x => Assert.Equal(taker, x.HolderPlayerId));
    }

    [Fact]
    public async Task TakeResponsibility_IsLostWhenTheTargetHasNothing()
    {
        var (_, card, target) = await WithTakeResponsibilityAsync();

        var result = await TakeResponsibilityAsync(card.HolderPlayerId, card.CardId, target, null);

        Assert.Null(result.Failure);
        Assert.Equal(ResistancePlotCardLocation.Played, (await ReloadAsync()).PlotCards.Single(x => x.CardId == card.CardId).Location);
    }

    [Fact]
    public async Task TakeResponsibility_RefusesTheWrongCallerTargetAndStep()
    {
        var ct = CancellationToken.None;
        var (game, card, target) = await WithTakeResponsibilityAsync();

        Assert.Equal(ResistanceFailure.NotYourCard, (await TakeResponsibilityAsync(target, card.CardId, card.HolderPlayerId!.Value, null)).Failure);
        Assert.Equal(ResistanceFailure.InvalidTarget, (await TakeResponsibilityAsync(card.HolderPlayerId, card.CardId, card.HolderPlayerId!.Value, null)).Failure);
        Assert.Equal(ResistanceFailure.InvalidTarget, (await TakeResponsibilityAsync(card.HolderPlayerId, card.CardId, Guid.NewGuid(), null)).Failure);

        var team = game.Players.OrderBy(x => x.Order).Take(game.Rules.TeamSize(5, 1)).Select(x => x.PlayerId).ToList();
        await SelectTeamAsync(game.LeaderPlayerId, team);
        var voting = await TakeResponsibilityAsync(card.HolderPlayerId, card.CardId, target, null);
        Assert.Equal(ResistanceFailure.CardNotPlayableNow, voting.Failure);

        foreach (var player in game.Players)
        {
            await CastVoteAsync(player.PlayerId, true);
        }

        await PassWindowsAsync();

        Assert.Null((await TakeResponsibilityAsync(card.HolderPlayerId, card.CardId, target, null)).Failure);
        Assert.Equal(ResistanceFailure.CardState, (await TakeResponsibilityAsync(card.HolderPlayerId, card.CardId, target, null)).Failure);
    }

    /// <summary>A game whose first team was rejected while a Strong Leader waited in the hand of a player who is not the next leader.</summary>
    private async Task<(ResistanceGame Game, ResistancePlotCard Card)> InLeaderWindowAsync()
    {
        var ct = CancellationToken.None;
        var game = await StartAsync(5, plotCards: true, onlyType: ResistancePlotCardType.StrongLeader);
        await DealAsync(game);
        game = await ReloadAsync();
        var card = game.PlotCards.Single(x => x.Location == ResistancePlotCardLocation.Hand);
        var next = ResistanceRoles.NextLeader(game);
        var holder = game.Players.First(x => x.PlayerId != game.LeaderPlayerId && x.PlayerId != next).PlayerId;
        await SetCardAsync(card.CardId, holder, ResistancePlotCardLocation.Hand);

        await SelectTeamAndVoteAsync(await ReloadAsync(), approve: false, passWindows: false);
        game = await ReloadAsync();
        Assert.Equal(ResistancePhase.LeaderWindow, game.Phase);

        return (game, game.PlotCards.Single(x => x.CardId == card.CardId));
    }

    [Fact]
    public async Task StrongLeader_OpensAWindowAfterARejectionAndTheHolderTakesTheLead()
    {
        var (game, card) = await InLeaderWindowAsync();

        var taken = await TakeLeadAsync(card.HolderPlayerId, card.CardId);

        Assert.Null(taken.Failure);
        var after = await ReloadAsync();
        Assert.Equal(card.HolderPlayerId, after.LeaderPlayerId);
        Assert.Equal(ResistancePhase.SelectingTeam, after.Phase);
        var played = after.PlotCards.Single(x => x.CardId == card.CardId);
        Assert.Equal((ResistancePlotCardLocation.Played, true), (played.Location, played.IsConfirmed));
        Assert.NotEqual(game.LeaderPlayerId, after.LeaderPlayerId);
    }

    [Fact]
    public async Task StrongLeader_WindowEndsWhenEveryoneConfirmedAndKeepsTheLeader()
    {
        var ct = CancellationToken.None;
        var (game, _) = await InLeaderWindowAsync();
        var leader = game.LeaderPlayerId;

        foreach (var player in game.Players.Take(4))
        {
            Assert.Null((await PassLeaderWindowAsync(player.PlayerId)).Failure);
        }

        Assert.Equal(ResistancePhase.LeaderWindow, (await ReloadAsync()).Phase);
        await PassLeaderWindowAsync(game.Players[4].PlayerId);
        var after = await ReloadAsync();
        Assert.Equal((ResistancePhase.SelectingTeam, leader), (after.Phase, after.LeaderPlayerId));
    }

    [Fact]
    public async Task StrongLeader_RefusesOtherCallersAndStepsAndTheLeaderTakingTheirOwnLead()
    {
        var ct = CancellationToken.None;
        var (game, card) = await InLeaderWindowAsync();
        var stranger = game.Players.First(x => x.PlayerId != card.HolderPlayerId).PlayerId;

        Assert.Equal(ResistanceFailure.NotYourCard, (await TakeLeadAsync(stranger, card.CardId)).Failure);
        Assert.Equal(ResistanceFailure.NotYourCard, (await TakeLeadAsync(card.HolderPlayerId, 99)).Failure);
        Assert.Equal(ResistanceFailure.NotPlayerToConfirm, (await PassLeaderWindowAsync(Guid.NewGuid())).Failure);

        await TakeLeadAsync(card.HolderPlayerId, card.CardId);
        Assert.Equal(ResistanceFailure.CardState, (await TakeLeadAsync(card.HolderPlayerId, card.CardId)).Failure);
        Assert.Equal(ResistanceFailure.CardNotPlayableNow, (await PassLeaderWindowAsync(stranger)).Failure);
    }

    /// <summary>A game whose first team was approved while a No Confidence waited in a hand.</summary>
    private async Task<(ResistanceGame Game, ResistancePlotCard Card)> InConfidenceWindowAsync()
    {
        var game = await StartAsync(5, plotCards: true, onlyType: ResistancePlotCardType.NoConfidence);
        await DealAsync(game);
        await SelectTeamAndVoteAsync(await ReloadAsync(), approve: true, passWindows: false);
        game = await ReloadAsync();
        Assert.Equal(ResistancePhase.ConfidenceWindow, game.Phase);

        return (game, game.PlotCards.Single(x => x.Location == ResistancePlotCardLocation.Hand));
    }

    [Fact]
    public async Task NoConfidence_CancelsTheApprovalAndCountsAsARejectedTeam()
    {
        var (game, card) = await InConfidenceWindowAsync();

        var played = await PlayNoConfidenceAsync(card.HolderPlayerId, card.CardId);

        Assert.Null(played.Failure);
        var after = await ReloadAsync();
        Assert.Equal(1, after.RejectedTeams);
        Assert.Equal(ResistancePhase.SelectingTeam, after.Phase);
        Assert.Equal(1, after.MissionNumber);
        Assert.NotEqual(game.LeaderPlayerId, after.LeaderPlayerId);
        Assert.DoesNotContain(after.Players, x => x.IsOnTeam);
        Assert.Equal(ResistancePlotCardLocation.Played, after.PlotCards.Single(x => x.CardId == card.CardId).Location);
    }

    [Fact]
    public async Task NoConfidence_TheMissionStartsOnlyWhenEveryoneConfirmed()
    {
        var ct = CancellationToken.None;
        var (game, _) = await InConfidenceWindowAsync();

        foreach (var player in game.Players.Take(4))
        {
            await PassLeaderWindowAsync(player.PlayerId);
        }

        Assert.Equal(ResistancePhase.ConfidenceWindow, (await ReloadAsync()).Phase);
        await PassLeaderWindowAsync(game.Players[4].PlayerId);
        var after = await ReloadAsync();
        Assert.Equal((ResistancePhase.PlayingMission, 0), (after.Phase, after.RejectedTeams));
    }

    [Fact]
    public async Task NoConfidence_TheFifthRejectionEndsTheGameForTheSpies()
    {
        var (game, card) = await InConfidenceWindowAsync();
        await using (var dbContext = await _writer.CreateDbContextAsync())
        {
            var row = await dbContext.Set<ResistanceGame>().SingleAsync(x => x.SessionId == _sessionId);
            row.RejectedTeams = ResistanceRoles.MaxRejectedTeams - 1;
            await dbContext.SaveChangesAsync();
        }

        await PlayNoConfidenceAsync(card.HolderPlayerId, card.CardId);

        var after = await ReloadAsync();
        Assert.Equal((ResistancePhase.GameOver, ResistanceRole.Spy), (after.Phase, after.Winner));
    }

    [Fact]
    public async Task NoConfidence_RefusesOtherCallersAndStepsAndAReplay()
    {
        var ct = CancellationToken.None;
        var (game, card) = await InConfidenceWindowAsync();
        var stranger = game.Players.First(x => x.PlayerId != card.HolderPlayerId).PlayerId;

        Assert.Equal(ResistanceFailure.NotYourCard, (await PlayNoConfidenceAsync(stranger, card.CardId)).Failure);
        await PlayNoConfidenceAsync(card.HolderPlayerId, card.CardId);
        Assert.Equal(ResistanceFailure.CardState, (await PlayNoConfidenceAsync(card.HolderPlayerId, card.CardId)).Failure);
    }

    /// <summary>A game whose first mission was played (everybody on the team played Success) while Keeping a Close Eye was in a hand.</summary>
    private async Task<(ResistanceGame Game, ResistancePlotCard Card)> AfterTheMissionWithCloseEyeAsync()
    {
        var game = await StartAsync(5, plotCards: true, onlyType: ResistancePlotCardType.KeepingCloseEye);
        await DealAsync(game);
        await SelectTeamAndVoteAsync(await ReloadAsync(), approve: true);
        game = await ReloadAsync();
        foreach (var member in game.Players.Where(x => x.IsOnTeam))
        {
            await PlayMissionCardAsync(member.PlayerId, true);
        }

        game = await ReloadAsync();
        Assert.Equal(ResistancePhase.PeekWindow, game.Phase);

        return (game, game.PlotCards.Single(x => x.Location == ResistancePlotCardLocation.Hand));
    }

    [Fact]
    public async Task KeepingCloseEye_LetsTheHolderSeeATeamMembersCardAndNobodyElse()
    {
        var (game, card) = await AfterTheMissionWithCloseEyeAsync();
        var target = game.Players.First(x => x.IsOnTeam && x.PlayerId != card.HolderPlayerId).PlayerId;

        var peeked = await PeekMissionCardAsync(card.HolderPlayerId, card.CardId, target);

        Assert.Null(peeked.Failure);
        var after = await ReloadAsync();
        Assert.Equal([(target, true)], ResistanceRoles.PeekedCards(after, card.HolderPlayerId!.Value));
        Assert.Empty(ResistanceRoles.PeekedCards(after, target));
        Assert.Equal(ResistancePlotCardLocation.Played, after.PlotCards.Single(x => x.CardId == card.CardId).Location);
    }

    [Fact]
    public async Task KeepingCloseEye_RefusesYourOwnCardPlayersOffTheTeamTheWrongCallerAndStep()
    {
        var ct = CancellationToken.None;
        var (game, card) = await AfterTheMissionWithCloseEyeAsync();
        var holder = card.HolderPlayerId!.Value;
        var outsider = game.Players.First(x => !x.IsOnTeam && x.PlayerId != holder).PlayerId;
        var member = game.Players.First(x => x.IsOnTeam && x.PlayerId != holder).PlayerId;

        Assert.Equal(ResistanceFailure.InvalidTarget, (await PeekMissionCardAsync(holder, card.CardId, holder)).Failure);
        Assert.Equal(ResistanceFailure.InvalidTarget, (await PeekMissionCardAsync(holder, card.CardId, outsider)).Failure);
        Assert.Equal(ResistanceFailure.NotYourCard, (await PeekMissionCardAsync(member, card.CardId, member)).Failure);
        Assert.Null((await PeekMissionCardAsync(holder, card.CardId, member)).Failure);
        Assert.Equal(ResistanceFailure.CardState, (await PeekMissionCardAsync(holder, card.CardId, member)).Failure);
    }

    [Fact]
    public async Task KeepingCloseEye_CannotBePlayedBeforeTheMissionIsPlayedOrAfterTheNextStarts()
    {
        var ct = CancellationToken.None;
        var game = await StartAsync(5, plotCards: true, onlyType: ResistancePlotCardType.KeepingCloseEye);
        await DealAsync(game);
        game = await ReloadAsync();
        var card = game.PlotCards.Single(x => x.Location == ResistancePlotCardLocation.Hand);
        var other = game.Players.First(x => x.PlayerId != card.HolderPlayerId).PlayerId;

        var early = await PeekMissionCardAsync(card.HolderPlayerId, card.CardId, other);

        Assert.Equal(ResistanceFailure.CardNotPlayableNow, early.Failure);
    }

    [Fact]
    public async Task PeekWindow_BlocksContinueUntilEveryoneConfirmedAndThenEndsThePeeking()
    {
        var ct = CancellationToken.None;
        var (game, card) = await AfterTheMissionWithCloseEyeAsync();
        var holder = card.HolderPlayerId!.Value;
        var target = game.Players.First(x => x.IsOnTeam && x.PlayerId != holder).PlayerId;

        Assert.Equal(ResistanceFailure.NothingToContinue, (await ContinueAsync(game.LeaderPlayerId, true)).Failure);
        Assert.Single((await ReloadAsync()).MissionResults);
        foreach (var player in game.Players.Take(4))
        {
            await PassLeaderWindowAsync(player.PlayerId);
        }

        Assert.Equal(ResistancePhase.PeekWindow, (await ReloadAsync()).Phase);
        await PassLeaderWindowAsync(game.Players[4].PlayerId);

        Assert.Equal(ResistancePhase.MissionResult, (await ReloadAsync()).Phase);
        Assert.Equal(ResistanceFailure.CardNotPlayableNow, (await PeekMissionCardAsync(holder, card.CardId, target)).Failure);
        Assert.Null((await ContinueAsync(game.LeaderPlayerId, true)).Failure);
    }

    [Fact]
    public async Task PeekWindow_IsSkippedWhenNoPlayableCardIsInAHand()
    {
        var game = await StartAsync(5, plotCards: false);
        await SelectTeamAndVoteAsync(game, approve: true);
        game = await ReloadAsync();
        foreach (var member in game.Players.Where(x => x.IsOnTeam))
        {
            await PlayMissionCardAsync(member.PlayerId, true);
        }

        Assert.Equal(ResistancePhase.MissionResult, (await ReloadAsync()).Phase);
    }

    [Fact]
    public async Task KeepingCloseEye_ASecondHolderCannotLookAtTheSameCard()
    {
        var ct = CancellationToken.None;
        var (game, card) = await AfterTheMissionWithCloseEyeAsync();
        var holder = card.HolderPlayerId!.Value;
        var target = game.Players.First(x => x.IsOnTeam && x.PlayerId != holder).PlayerId;
        var second = game.Players.First(x => x.PlayerId != holder && x.PlayerId != target).PlayerId;
        await SetCardAsync(SpareCardId(game), second, ResistancePlotCardLocation.Hand, ResistancePlotCardType.KeepingCloseEye);
        await PeekMissionCardAsync(holder, card.CardId, target);

        var again = await PeekMissionCardAsync(second, SpareCardId(game), target);

        Assert.Equal(ResistanceFailure.InvalidTarget, again.Failure);
    }

    [Theory]
    [InlineData(ResistancePlotCardType.InTheSpotlight)]
    [InlineData(ResistancePlotCardType.TakeResponsibility)]
    [InlineData(ResistancePlotCardType.KeepingCloseEye)]
    public async Task AnyCardInAHand_OpensTheConfidenceWindowSoItNeverTellsWhichCardIsOut(ResistancePlotCardType type)
    {
        var game = await StartAsync(5, plotCards: true, onlyType: type);
        await DealAsync(game);

        await SelectTeamAndVoteAsync(await ReloadAsync(), approve: true, passWindows: false);

        Assert.Equal(ResistancePhase.ConfidenceWindow, (await ReloadAsync()).Phase);
    }

    [Fact]
    public async Task WithoutPlayableCardsInAHand_NoWindowOpens()
    {
        var game = await StartAsync(5, plotCards: true, onlyOpinionMakers: true);
        await DealAsync(game);

        await SelectTeamAndVoteAsync(await ReloadAsync(), approve: true, passWindows: false);

        Assert.Equal(ResistancePhase.PlayingMission, (await ReloadAsync()).Phase);
    }
}
