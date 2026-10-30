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

    /// <param name="withoutImmediateCards">Swaps the immediate cards for No Confidence, so a test can count on every drawn
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
            game.Rules.PlotDeck[ResistancePlotCardType.NoConfidence] = 7;
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

    private async Task SelectTeamAndVoteAsync(ResistanceGame game, bool approve)
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

        await _repository.ContinueAsync(_sessionId, game.LeaderPlayerId, true, ct);

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
        Assert.Equal(ResistanceRestartStatus.Restarted, (await _repository.RestartAsync(_sessionId, DateTimeOffset.UtcNow, ct)).Status);
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
        Assert.Equal(ResistanceRestartStatus.Restarted, (await _repository.RestartAsync(_sessionId, DateTimeOffset.UtcNow, ct)).Status);
        var restarted = await ReloadAsync();
        Assert.DoesNotContain(restarted.PlotCards, x => x.Location == ResistancePlotCardLocation.Played || x.IsConfirmed || x.TargetPlayerId is not null);
    }

    /// <summary>A started 5-player game whose approved team plays the mission, with the one drawn In the Spotlight dealt.</summary>
    private async Task<(ResistanceGame Game, ResistancePlotCard Card)> PlayingWithSpotlightAsync()
    {
        var game = await StartAsync(5, plotCards: true, onlyType: ResistancePlotCardType.InTheSpotlight);
        await DealAsync(game);
        await SelectTeamAndVoteAsync(await ReloadAsync(), approve: true);
        game = await ReloadAsync();
        Assert.Equal(ResistancePhase.PlayingMission, game.Phase);

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
        Assert.Equal(ResistancePhase.PlayingMission, after.Phase);
    }

    [Fact]
    public async Task InTheSpotlight_IsPlayedOnlyWhileTheMissionIsPlayed()
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
    public async Task InTheSpotlight_RejectsTargetsOutsideTheTeamPlayersWhoPlayedAndTheHolder()
    {
        var ct = CancellationToken.None;
        var (game, card) = await PlayingWithSpotlightAsync();
        var outsider = game.Players.First(x => !x.IsOnTeam && x.PlayerId != card.HolderPlayerId);
        var member = game.Players.First(x => x.IsOnTeam && x.PlayerId != card.HolderPlayerId);
        await _repository.PlayMissionCardAsync(_sessionId, member.PlayerId, true, ct);

        foreach (var invalid in new[] { outsider.PlayerId, member.PlayerId, card.HolderPlayerId!.Value, Guid.NewGuid() })
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
}
