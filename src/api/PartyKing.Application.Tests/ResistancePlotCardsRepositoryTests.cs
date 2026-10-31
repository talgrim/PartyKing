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
        var started = await _repository.StartAsync(_sessionId, DateTimeOffset.UtcNow, CancellationToken.None);
        Assert.Equal(ResistanceStartStatus.Started, started.Status);

        return await ReloadAsync();
    }

    /// <summary>In a hand or face up in front of a player (an Opinion Maker): dealt out either way.</summary>
    private static bool IsHeld(ResistancePlotCard card) =>
        card.Location is ResistancePlotCardLocation.Hand or ResistancePlotCardLocation.InPlay;

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
            Assert.Equal(ResistancePlayCardStatus.Done, (await _repository.PlayPlotCardAsync(_sessionId, card.HolderPlayerId, card.CardId, target, ct)).Status);
        }

        current = await ReloadAsync();
        foreach (var card in ResistanceRoles.UnconfirmedCards(current).ToList())
        {
            Assert.Equal(ResistancePlayCardStatus.Done, (await _repository.ConfirmPlotCardAsync(_sessionId, ResistanceRoles.Viewer(card), card.CardId, ct)).Status);
        }
    }

    /// <summary>Hands the drawn cards out and leaves the immediate ones unplayed.</summary>
    private async Task DealOnlyAsync(ResistanceGame game)
    {
        var dealt = await _repository.DealPlotCardsAsync(
            _sessionId, game.LeaderPlayerId, DealEvenly(game), CancellationToken.None);
        Assert.Equal(ResistanceDealStatus.Dealt, dealt.Status);
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
                await _repository.PassLeaderWindowAsync(_sessionId, player.PlayerId, CancellationToken.None);
            }
        }
    }

    /// <param name="passWindows">Confirms the window a vote may open (any card in a hand opens one), so the round goes on.</param>
    private async Task SelectTeamAndVoteAsync(ResistanceGame game, bool approve, bool passWindows = true)
    {
        var ct = CancellationToken.None;
        var size = game.Rules.TeamSize(game.Players.Count, game.MissionNumber);
        var team = game.Players.OrderBy(x => x.Order).Take(size).Select(x => x.PlayerId).ToList();
        Assert.Equal(ResistanceTeamStatus.Selected, (await _repository.SelectTeamAsync(_sessionId, game.LeaderPlayerId, team, ct)).Status);
        var makers = ResistanceRoles.OpinionMakerIds(game);
        foreach (var player in game.Players.OrderBy(x => makers.Contains(x.PlayerId) ? 0 : 1))
        {
            await _repository.VoteAsync(_sessionId, player.PlayerId, approve, ct);
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

        var early = await _repository.SelectTeamAsync(_sessionId, game.LeaderPlayerId, team, CancellationToken.None);

        Assert.Equal(ResistanceTeamStatus.CardsNotDealt, early.Status);
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
        var game = await StartAsync(7, plotCards: true, withoutImmediateCards: true);
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

        var keptAway = await _repository.DealPlotCardsAsync(
            _sessionId, game.LeaderPlayerId,
            [new PlotCardAssignment(toDeal[0].CardId, other), new PlotCardAssignment(drawn[0].CardId, other)], ct);
        Assert.Equal(ResistanceDealStatus.UnknownCard, keptAway.Status);

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
            await _repository.PlayMissionCardAsync(_sessionId, member.PlayerId, true, ct);
        }

        await PassWindowsAsync();
        await _repository.ContinueAsync(_sessionId, game.LeaderPlayerId, true, ct);
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

        var restarted = await _repository.RestartAsync(_sessionId, CancellationToken.None);

        Assert.Equal(ResistanceRestartStatus.Restarted, restarted.Status);
        Assert.Empty((await ReloadAsync()).PlotCards);

        await _repository.StartAsync(_sessionId, DateTimeOffset.UtcNow, CancellationToken.None);
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
        await _repository.SelectTeamAsync(_sessionId, game.LeaderPlayerId, team, ct);

        Assert.Equal(ResistanceVoteStatus.OpinionMakersFirst, (await _repository.VoteAsync(_sessionId, other, true, ct)).Status);
        Assert.Equal(ResistanceVoteStatus.Voted, (await _repository.VoteAsync(_sessionId, maker, false, ct)).Status);
        Assert.Equal(ResistanceVoteStatus.VoteLocked, (await _repository.VoteAsync(_sessionId, maker, true, ct)).Status);

        Assert.Equal(ResistanceVoteStatus.Voted, (await _repository.VoteAsync(_sessionId, other, true, ct)).Status);
        // Players other than the maker can still change their mind
        Assert.Equal(ResistanceVoteStatus.Voted, (await _repository.VoteAsync(_sessionId, other, false, ct)).Status);
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
        await _repository.SelectTeamAsync(_sessionId, game.LeaderPlayerId, team, ct);

        await _repository.VoteAsync(_sessionId, makers[0], true, ct);
        Assert.Equal(ResistanceVoteStatus.OpinionMakersFirst, (await _repository.VoteAsync(_sessionId, other, true, ct)).Status);

        await _repository.VoteAsync(_sessionId, makers[1], true, ct);
        Assert.Equal(ResistanceVoteStatus.Voted, (await _repository.VoteAsync(_sessionId, other, true, ct)).Status);
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
        Assert.Equal(ResistanceRestartStatus.Restarted, (await _repository.RestartAsync(_sessionId, ct)).Status);
        Assert.Empty(ResistanceRoles.OpinionMakerIds(await ReloadAsync()));
    }

    [Fact]
    public async Task DealingAnImmediateCard_StartsPlayingItBeforeTheTeamIsPicked()
    {
        var game = await StartAsync(5, plotCards: true, onlyType: ResistancePlotCardType.OpenUp);

        await DealOnlyAsync(game);

        var after = await ReloadAsync();
        Assert.Equal(ResistancePhase.ResolvingCards, after.Phase);
        Assert.Equal(ResistanceTeamStatus.CardsNotDealt, (await _repository.SelectTeamAsync(
            _sessionId, after.LeaderPlayerId, after.Players.Take(2).Select(x => x.PlayerId).ToList(), CancellationToken.None)).Status);
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

        Assert.Equal(ResistancePlayCardStatus.InvalidTarget, (await _repository.PlayPlotCardAsync(_sessionId, holder, card.CardId, farAway, ct)).Status);
        Assert.Equal(ResistancePlayCardStatus.InvalidTarget, (await _repository.PlayPlotCardAsync(_sessionId, holder, card.CardId, holder, ct)).Status);
        Assert.Equal(ResistancePlayCardStatus.NotAllowed, (await _repository.PlayPlotCardAsync(_sessionId, farAway, card.CardId, neighbours[0], ct)).Status);
        Assert.Equal(ResistancePlayCardStatus.WrongState, (await _repository.ConfirmPlotCardAsync(_sessionId, holder, card.CardId, ct)).Status);

        Assert.Equal(ResistancePlayCardStatus.Done, (await _repository.PlayPlotCardAsync(_sessionId, holder, card.CardId, neighbours[1], ct)).Status);

        var played = await ReloadAsync();
        var log = played.PlotCards.Single(x => x.CardId == card.CardId);
        Assert.Equal(ResistancePlotCardLocation.Played, log.Location);
        Assert.Equal(neighbours[1], log.TargetPlayerId);
        Assert.Equal(1, log.PlayedInMission);
        Assert.Equal(ResistancePhase.ResolvingCards, played.Phase);
        Assert.Equal(ResistancePlayCardStatus.WrongState, (await _repository.PlayPlotCardAsync(_sessionId, holder, card.CardId, neighbours[0], ct)).Status);
        // The holder sees the neighbour's identity card, so nobody else may confirm it
        Assert.Equal(ResistancePlayCardStatus.NotAllowed, (await _repository.ConfirmPlotCardAsync(_sessionId, neighbours[1], card.CardId, ct)).Status);

        Assert.Equal(ResistancePlayCardStatus.Done, (await _repository.ConfirmPlotCardAsync(_sessionId, holder, card.CardId, ct)).Status);
        Assert.Equal(ResistancePhase.SelectingTeam, (await ReloadAsync()).Phase);
        Assert.Equal(ResistancePlayCardStatus.NotResolving, (await _repository.ConfirmPlotCardAsync(_sessionId, holder, card.CardId, ct)).Status);
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

        Assert.Equal(ResistancePlayCardStatus.InvalidTarget, (await _repository.PlayPlotCardAsync(_sessionId, holder, card.CardId, holder, ct)).Status);
        Assert.Equal(ResistancePlayCardStatus.InvalidTarget, (await _repository.PlayPlotCardAsync(_sessionId, holder, card.CardId, Guid.NewGuid(), ct)).Status);
        Assert.Equal(ResistancePlayCardStatus.Done, (await _repository.PlayPlotCardAsync(_sessionId, holder, card.CardId, chosen, ct)).Status);

        var played = (await ReloadAsync()).PlotCards.Single(x => x.CardId == card.CardId);
        Assert.Equal(chosen, ResistanceRoles.Viewer(played));
        Assert.Equal(holder, ResistanceRoles.Revealed(played));
        Assert.Equal(ResistancePlayCardStatus.NotAllowed, (await _repository.ConfirmPlotCardAsync(_sessionId, holder, card.CardId, ct)).Status);
        Assert.Equal(ResistancePlayCardStatus.Done, (await _repository.ConfirmPlotCardAsync(_sessionId, chosen, card.CardId, ct)).Status);
        Assert.Equal(ResistancePhase.SelectingTeam, (await ReloadAsync()).Phase);
    }

    [Fact]
    public async Task OpenUp_MayBeShownToTheLeader()
    {
        var game = await StartAsync(5, plotCards: true, onlyType: ResistancePlotCardType.OpenUp);
        await DealOnlyAsync(game);
        var card = ResistanceRoles.UnplayedImmediateCards(await ReloadAsync()).Single();

        var played = await _repository.PlayPlotCardAsync(
            _sessionId, card.HolderPlayerId, card.CardId, game.LeaderPlayerId!.Value, CancellationToken.None);

        Assert.Equal(ResistancePlayCardStatus.Done, played.Status);
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
        Assert.Equal(ResistancePlayCardStatus.NotAllowed, (await _repository.PlayPlotCardAsync(_sessionId, chosen, card.CardId, leader, ct)).Status);
        Assert.Equal(ResistancePlayCardStatus.InvalidTarget, (await _repository.PlayPlotCardAsync(_sessionId, leader, card.CardId, leader, ct)).Status);
        Assert.Equal(ResistancePlayCardStatus.Done, (await _repository.PlayPlotCardAsync(_sessionId, leader, card.CardId, chosen, ct)).Status);
        Assert.Equal(ResistancePlayCardStatus.NotAllowed, (await _repository.ConfirmPlotCardAsync(_sessionId, leader, card.CardId, ct)).Status);
        Assert.Equal(ResistancePlayCardStatus.Done, (await _repository.ConfirmPlotCardAsync(_sessionId, chosen, card.CardId, ct)).Status);
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
        await _repository.PlayPlotCardAsync(_sessionId, cards[1].HolderPlayerId, cards[1].CardId, targets[1], ct);
        await _repository.ConfirmPlotCardAsync(_sessionId, targets[1], cards[1].CardId, ct);
        Assert.Equal(ResistancePhase.ResolvingCards, (await ReloadAsync()).Phase);

        await _repository.PlayPlotCardAsync(_sessionId, cards[0].HolderPlayerId, cards[0].CardId, targets[0], ct);
        Assert.Equal(ResistancePhase.ResolvingCards, (await ReloadAsync()).Phase);

        await _repository.ConfirmPlotCardAsync(_sessionId, targets[0], cards[0].CardId, ct);
        Assert.Equal(ResistancePhase.SelectingTeam, (await ReloadAsync()).Phase);
    }

    [Fact]
    public async Task PlayingOrConfirming_IsRefusedOutsideThePlayingPhase()
    {
        var ct = CancellationToken.None;
        var game = await StartAsync(5, plotCards: true, onlyType: ResistancePlotCardType.OpenUp);
        var someone = game.Players.First(x => x.PlayerId != game.LeaderPlayerId).PlayerId;

        Assert.Equal(ResistancePlayCardStatus.NotResolving, (await _repository.PlayPlotCardAsync(_sessionId, someone, 0, game.LeaderPlayerId!.Value, ct)).Status);
        Assert.Equal(ResistancePlayCardStatus.NoGame, (await _repository.ConfirmPlotCardAsync(Guid.NewGuid(), someone, 0, ct)).Status);

        await DealOnlyAsync(game);
        Assert.Equal(ResistancePlayCardStatus.UnknownCard, (await _repository.PlayPlotCardAsync(_sessionId, someone, 99, game.LeaderPlayerId!.Value, ct)).Status);
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
        Assert.Equal(ResistanceRestartStatus.Restarted, (await _repository.RestartAsync(_sessionId, ct)).Status);
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

        var played = await _repository.PlayPlotCardAsync(_sessionId, card.HolderPlayerId, card.CardId, target.PlayerId, ct);

        Assert.Equal(ResistancePlayCardStatus.Done, played.Status);
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

        Assert.Equal(ResistanceMissionStatus.NotPlaying, (await _repository.PlayMissionCardAsync(_sessionId, member.PlayerId, true, ct)).Status);
        foreach (var player in game.Players.Take(4))
        {
            await _repository.PassLeaderWindowAsync(_sessionId, player.PlayerId, ct);
        }

        Assert.Equal(ResistancePhase.ConfidenceWindow, (await ReloadAsync()).Phase);
        await _repository.PassLeaderWindowAsync(_sessionId, game.Players[4].PlayerId, ct);

        Assert.Equal(ResistancePhase.PlayingMission, (await ReloadAsync()).Phase);
        Assert.Equal(ResistanceMissionStatus.Played, (await _repository.PlayMissionCardAsync(_sessionId, member.PlayerId, true, ct)).Status);
    }

    [Fact]
    public async Task NoConfidence_GivesBackAnInTheSpotlightPlayedInTheSameWindow()
    {
        var ct = CancellationToken.None;
        var (game, spotlight) = await PlayingWithSpotlightAsync();
        var target = game.Players.First(x => x.IsOnTeam && x.PlayerId != spotlight.HolderPlayerId).PlayerId;
        await _repository.PlayPlotCardAsync(_sessionId, spotlight.HolderPlayerId, spotlight.CardId, target, ct);
        var other = game.Players.First(x => x.PlayerId != spotlight.HolderPlayerId).PlayerId;
        await SetCardAsync(SpareCardId(game), other, ResistancePlotCardLocation.Hand, ResistancePlotCardType.NoConfidence);

        var played = await _repository.PlayNoConfidenceAsync(_sessionId, other, SpareCardId(game), ct);

        Assert.Equal(ResistancePlayCardStatus.Done, played.Status);
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

        var late = await _repository.PlayPlotCardAsync(_sessionId, card.HolderPlayerId, card.CardId, target, ct);

        Assert.Equal(ResistancePlayCardStatus.NotPlayingMission, late.Status);
    }

    [Fact]
    public async Task InTheSpotlight_IsPlayedOnlyInTheConfidenceWindow()
    {
        var ct = CancellationToken.None;
        var game = await StartAsync(5, plotCards: true, onlyType: ResistancePlotCardType.InTheSpotlight);
        await DealAsync(game);
        var card = (await ReloadAsync()).PlotCards.Single(x => x.Location == ResistancePlotCardLocation.Hand);
        var target = game.Players.First(x => x.PlayerId != card.HolderPlayerId).PlayerId;

        var early = await _repository.PlayPlotCardAsync(_sessionId, card.HolderPlayerId, card.CardId, target, ct);

        Assert.Equal(ResistancePlayCardStatus.NotPlayingMission, early.Status);
    }

    [Fact]
    public async Task InTheSpotlight_RejectsTargetsOutsideTheTeamAndTheHolder()
    {
        var ct = CancellationToken.None;
        var (game, card) = await PlayingWithSpotlightAsync();
        var outsider = game.Players.First(x => !x.IsOnTeam && x.PlayerId != card.HolderPlayerId);

        foreach (var invalid in new[] { outsider.PlayerId, card.HolderPlayerId!.Value, Guid.NewGuid() })
        {
            var result = await _repository.PlayPlotCardAsync(_sessionId, card.HolderPlayerId, card.CardId, invalid, ct);
            Assert.Equal(ResistancePlayCardStatus.InvalidTarget, result.Status);
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

        var byStranger = await _repository.PlayPlotCardAsync(_sessionId, stranger, card.CardId, target, ct);
        await _repository.PlayPlotCardAsync(_sessionId, card.HolderPlayerId, card.CardId, target, ct);
        var again = await _repository.PlayPlotCardAsync(_sessionId, card.HolderPlayerId, card.CardId, target, ct);

        Assert.Equal(ResistancePlayCardStatus.NotAllowed, byStranger.Status);
        Assert.Equal(ResistancePlayCardStatus.WrongState, again.Status);
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

        var result = await _repository.TakeResponsibilityAsync(_sessionId, card.HolderPlayerId, card.CardId, target, null, CancellationToken.None);

        Assert.Equal(ResistancePlayCardStatus.Done, result.Status);
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

        await _repository.TakeResponsibilityAsync(_sessionId, card.HolderPlayerId, card.CardId, target, null, CancellationToken.None);

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

        var unanswered = await _repository.TakeResponsibilityAsync(_sessionId, card.HolderPlayerId, card.CardId, target, null, CancellationToken.None);
        var answered = await _repository.TakeResponsibilityAsync(_sessionId, card.HolderPlayerId, card.CardId, target, takeOpinionMaker, CancellationToken.None);

        Assert.Equal(ResistancePlayCardStatus.ChoiceRequired, unanswered.Status);
        Assert.Equal(ResistancePlayCardStatus.Done, answered.Status);
        var after = await ReloadAsync();
        Assert.Equal(takeOpinionMaker ? card.HolderPlayerId : target, after.PlotCards.Single(x => x.CardId == maker).HolderPlayerId);
        Assert.Equal(takeOpinionMaker ? target : card.HolderPlayerId, after.PlotCards.Single(x => x.CardId == handCard).HolderPlayerId);
    }

    [Fact]
    public async Task TakeResponsibility_IsLostWhenTheTargetHasNothing()
    {
        var (_, card, target) = await WithTakeResponsibilityAsync();

        var result = await _repository.TakeResponsibilityAsync(_sessionId, card.HolderPlayerId, card.CardId, target, null, CancellationToken.None);

        Assert.Equal(ResistancePlayCardStatus.Done, result.Status);
        Assert.Equal(ResistancePlotCardLocation.Played, (await ReloadAsync()).PlotCards.Single(x => x.CardId == card.CardId).Location);
    }

    [Fact]
    public async Task TakeResponsibility_RefusesTheWrongCallerTargetAndStep()
    {
        var ct = CancellationToken.None;
        var (game, card, target) = await WithTakeResponsibilityAsync();

        Assert.Equal(ResistancePlayCardStatus.NotAllowed, (await _repository.TakeResponsibilityAsync(_sessionId, target, card.CardId, card.HolderPlayerId!.Value, null, ct)).Status);
        Assert.Equal(ResistancePlayCardStatus.InvalidTarget, (await _repository.TakeResponsibilityAsync(_sessionId, card.HolderPlayerId, card.CardId, card.HolderPlayerId!.Value, null, ct)).Status);
        Assert.Equal(ResistancePlayCardStatus.InvalidTarget, (await _repository.TakeResponsibilityAsync(_sessionId, card.HolderPlayerId, card.CardId, Guid.NewGuid(), null, ct)).Status);

        var team = game.Players.OrderBy(x => x.Order).Take(game.Rules.TeamSize(5, 1)).Select(x => x.PlayerId).ToList();
        await _repository.SelectTeamAsync(_sessionId, game.LeaderPlayerId, team, ct);
        var voting = await _repository.TakeResponsibilityAsync(_sessionId, card.HolderPlayerId, card.CardId, target, null, ct);
        Assert.Equal(ResistancePlayCardStatus.WrongPhase, voting.Status);

        foreach (var player in game.Players)
        {
            await _repository.VoteAsync(_sessionId, player.PlayerId, true, ct);
        }

        await PassWindowsAsync();

        Assert.Equal(ResistancePlayCardStatus.Done, (await _repository.TakeResponsibilityAsync(_sessionId, card.HolderPlayerId, card.CardId, target, null, ct)).Status);
        Assert.Equal(ResistancePlayCardStatus.WrongState, (await _repository.TakeResponsibilityAsync(_sessionId, card.HolderPlayerId, card.CardId, target, null, ct)).Status);
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

        var taken = await _repository.TakeLeadAsync(_sessionId, card.HolderPlayerId, card.CardId, CancellationToken.None);

        Assert.Equal(ResistancePlayCardStatus.Done, taken.Status);
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
            Assert.Equal(ResistancePlayCardStatus.Done, (await _repository.PassLeaderWindowAsync(_sessionId, player.PlayerId, ct)).Status);
        }

        Assert.Equal(ResistancePhase.LeaderWindow, (await ReloadAsync()).Phase);
        await _repository.PassLeaderWindowAsync(_sessionId, game.Players[4].PlayerId, ct);
        var after = await ReloadAsync();
        Assert.Equal((ResistancePhase.SelectingTeam, leader), (after.Phase, after.LeaderPlayerId));
    }

    [Fact]
    public async Task StrongLeader_RefusesOtherCallersAndStepsAndTheLeaderTakingTheirOwnLead()
    {
        var ct = CancellationToken.None;
        var (game, card) = await InLeaderWindowAsync();
        var stranger = game.Players.First(x => x.PlayerId != card.HolderPlayerId).PlayerId;

        Assert.Equal(ResistancePlayCardStatus.NotAllowed, (await _repository.TakeLeadAsync(_sessionId, stranger, card.CardId, ct)).Status);
        Assert.Equal(ResistancePlayCardStatus.UnknownCard, (await _repository.TakeLeadAsync(_sessionId, card.HolderPlayerId, 99, ct)).Status);
        Assert.Equal(ResistancePlayCardStatus.NotPlayer, (await _repository.PassLeaderWindowAsync(_sessionId, Guid.NewGuid(), ct)).Status);

        await _repository.TakeLeadAsync(_sessionId, card.HolderPlayerId, card.CardId, ct);
        Assert.Equal(ResistancePlayCardStatus.WrongState, (await _repository.TakeLeadAsync(_sessionId, card.HolderPlayerId, card.CardId, ct)).Status);
        Assert.Equal(ResistancePlayCardStatus.WrongPhase, (await _repository.PassLeaderWindowAsync(_sessionId, stranger, ct)).Status);
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

        var played = await _repository.PlayNoConfidenceAsync(_sessionId, card.HolderPlayerId, card.CardId, CancellationToken.None);

        Assert.Equal(ResistancePlayCardStatus.Done, played.Status);
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
            await _repository.PassLeaderWindowAsync(_sessionId, player.PlayerId, ct);
        }

        Assert.Equal(ResistancePhase.ConfidenceWindow, (await ReloadAsync()).Phase);
        await _repository.PassLeaderWindowAsync(_sessionId, game.Players[4].PlayerId, ct);
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

        await _repository.PlayNoConfidenceAsync(_sessionId, card.HolderPlayerId, card.CardId, CancellationToken.None);

        var after = await ReloadAsync();
        Assert.Equal((ResistancePhase.GameOver, ResistanceRole.Spy), (after.Phase, after.Winner));
    }

    [Fact]
    public async Task NoConfidence_RefusesOtherCallersAndStepsAndAReplay()
    {
        var ct = CancellationToken.None;
        var (game, card) = await InConfidenceWindowAsync();
        var stranger = game.Players.First(x => x.PlayerId != card.HolderPlayerId).PlayerId;

        Assert.Equal(ResistancePlayCardStatus.NotAllowed, (await _repository.PlayNoConfidenceAsync(_sessionId, stranger, card.CardId, ct)).Status);
        await _repository.PlayNoConfidenceAsync(_sessionId, card.HolderPlayerId, card.CardId, ct);
        Assert.Equal(ResistancePlayCardStatus.WrongState, (await _repository.PlayNoConfidenceAsync(_sessionId, card.HolderPlayerId, card.CardId, ct)).Status);
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
            await _repository.PlayMissionCardAsync(_sessionId, member.PlayerId, true, CancellationToken.None);
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

        var peeked = await _repository.PeekMissionCardAsync(_sessionId, card.HolderPlayerId, card.CardId, target, CancellationToken.None);

        Assert.Equal(ResistancePlayCardStatus.Done, peeked.Status);
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

        Assert.Equal(ResistancePlayCardStatus.InvalidTarget, (await _repository.PeekMissionCardAsync(_sessionId, holder, card.CardId, holder, ct)).Status);
        Assert.Equal(ResistancePlayCardStatus.InvalidTarget, (await _repository.PeekMissionCardAsync(_sessionId, holder, card.CardId, outsider, ct)).Status);
        Assert.Equal(ResistancePlayCardStatus.NotAllowed, (await _repository.PeekMissionCardAsync(_sessionId, member, card.CardId, member, ct)).Status);
        Assert.Equal(ResistancePlayCardStatus.Done, (await _repository.PeekMissionCardAsync(_sessionId, holder, card.CardId, member, ct)).Status);
        Assert.Equal(ResistancePlayCardStatus.WrongState, (await _repository.PeekMissionCardAsync(_sessionId, holder, card.CardId, member, ct)).Status);
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

        var early = await _repository.PeekMissionCardAsync(_sessionId, card.HolderPlayerId, card.CardId, other, ct);

        Assert.Equal(ResistancePlayCardStatus.WrongPhase, early.Status);
    }

    [Fact]
    public async Task PeekWindow_BlocksContinueUntilEveryoneConfirmedAndThenEndsThePeeking()
    {
        var ct = CancellationToken.None;
        var (game, card) = await AfterTheMissionWithCloseEyeAsync();
        var holder = card.HolderPlayerId!.Value;
        var target = game.Players.First(x => x.IsOnTeam && x.PlayerId != holder).PlayerId;

        Assert.Equal(ResistanceContinueStatus.NothingToContinue, (await _repository.ContinueAsync(_sessionId, game.LeaderPlayerId, true, ct)).Status);
        Assert.Single((await ReloadAsync()).MissionResults);
        foreach (var player in game.Players.Take(4))
        {
            await _repository.PassLeaderWindowAsync(_sessionId, player.PlayerId, ct);
        }

        Assert.Equal(ResistancePhase.PeekWindow, (await ReloadAsync()).Phase);
        await _repository.PassLeaderWindowAsync(_sessionId, game.Players[4].PlayerId, ct);

        Assert.Equal(ResistancePhase.MissionResult, (await ReloadAsync()).Phase);
        Assert.Equal(ResistancePlayCardStatus.WrongPhase, (await _repository.PeekMissionCardAsync(_sessionId, holder, card.CardId, target, ct)).Status);
        Assert.Equal(ResistanceContinueStatus.Continued, (await _repository.ContinueAsync(_sessionId, game.LeaderPlayerId, true, ct)).Status);
    }

    [Fact]
    public async Task PeekWindow_IsSkippedWhenNoPlayableCardIsInAHand()
    {
        var game = await StartAsync(5, plotCards: false);
        await SelectTeamAndVoteAsync(game, approve: true);
        game = await ReloadAsync();
        foreach (var member in game.Players.Where(x => x.IsOnTeam))
        {
            await _repository.PlayMissionCardAsync(_sessionId, member.PlayerId, true, CancellationToken.None);
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
        await _repository.PeekMissionCardAsync(_sessionId, holder, card.CardId, target, ct);

        var again = await _repository.PeekMissionCardAsync(_sessionId, second, SpareCardId(game), target, ct);

        Assert.Equal(ResistancePlayCardStatus.InvalidTarget, again.Status);
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
