using ErrorOr;
using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Admin.Services;
using PartyKing.Application.Games.Services.Resistance;
using PartyKing.Application.Games.Services;
using PartyKing.Application.Player.Services;
using PartyKing.Application.Realtime;
using PartyKing.Application.Sessions.Services;
using PartyKing.Contract.V1.Games.Resistance;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Infrastructure.Results;

namespace PartyKing.Application.Tests;

public class ResistancePlotCardServiceTests : ResistanceServiceTestBase
{
    [Fact]
    public async Task PlotCards_AreDrawnForTheLeaderAndDealtToOthers_HandsStayPrivateAndCountsPublic()
    {
        var ct = CancellationToken.None;
        await _lobbyService.CreateAsync(_id, Host, ct);
        await _lobbyService.SetPlotCardsAsync(_id, true, Host, ct);
        await _lobbyService.OpenAsync(_id, Admin, ct);
        var callers = await JoinPlayersAsync(5);
        var started = await _roundService.StartAsync(_id, Host, ct);
        var leader = callers.First(x => x.PlayerId == started.Value.LeaderPlayerId);
        var others = callers.Where(x => x.PlayerId != leader.PlayerId).ToList();

        Assert.Equal(ResistancePhaseDto.DealingCards, started.Value.Phase);
        Assert.Equal(1, started.Value.Rules.PlotCardsPerRound);
        var drawn = (await _lobbyService.GetAsync(_id, leader, ct)).Value.YourPlotCards;
        Assert.Single(drawn);
        foreach (var other in others)
        {
            Assert.Empty((await _lobbyService.GetAsync(_id, other, ct)).Value.YourPlotCards);
        }

        Assert.Equal("Resistance.CardsNotDealt", (await _roundService.SelectTeamAsync(_id, [leader.PlayerId!.Value], leader, ct)).FirstError.Code);
        Assert.Equal("Resistance.NotLeader", (await _plotCardService.DealPlotCardsAsync(_id, [], others[0], ct)).FirstError.Code);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _plotCardService.DealPlotCardsAsync(_id, [], Host, ct)).FirstError.Type);

        var receiver = others[0];
        var deal = drawn.Where(x => x.ToDeal).Select(x => new ResistancePlotCardAssignmentDto(x.Id, receiver.PlayerId!.Value)).ToList();
        var before = _notifier.ResistanceChanges.Count;
        var dealt = await _plotCardService.DealPlotCardsAsync(_id, deal, leader, ct);

        Assert.Equal(ResistancePhaseDto.SelectingTeam, dealt.Value.Phase);
        Assert.Equal(before + 1, _notifier.ResistanceChanges.Count);
        Assert.Equal(1, dealt.Value.PlotCardCounts.Sum(x => x.Count));
        Assert.Equal(deal.Count, (await _lobbyService.GetAsync(_id, receiver, ct)).Value.YourPlotCards.Count);
        foreach (var other in others.Skip(1))
        {
            Assert.Empty((await _lobbyService.GetAsync(_id, other, ct)).Value.YourPlotCards);
        }

        Assert.Equal("Resistance.NotDealing", (await _plotCardService.DealPlotCardsAsync(_id, [], leader, ct)).FirstError.Code);
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
        await _roundService.StartAsync(_id, Host, ct);
        _games.PlayCardStatus = status;

        var played = await _plotCardService.PlayPlotCardAsync(_id, 0, callers[1].PlayerId!.Value, callers[0], ct);
        var confirmed = await _plotCardService.ConfirmPlotCardAsync(_id, 0, callers[0], ct);

        Assert.Equal(code, played.FirstError.Code);
        Assert.Equal(code, confirmed.FirstError.Code);
    }

    [Fact]
    public async Task APlayedPlotCard_IsLoggedForEveryoneButItsIdentityCardOnlyShowsToItsViewer()
    {
        var ct = CancellationToken.None;
        var callers = await OpenGameAndJoinAsync(5);
        await _roundService.StartAsync(_id, Host, ct);
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

        var asHolder = (await _lobbyService.GetAsync(_id, CallerOf(holder), ct)).Value;
        var asTarget = (await _lobbyService.GetAsync(_id, CallerOf(target), ct)).Value;
        var asBystander = (await _lobbyService.GetAsync(_id, CallerOf(bystander), ct)).Value;
        var asHost = (await _lobbyService.GetAsync(_id, Host, ct)).Value;

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
        var after = (await _lobbyService.GetAsync(_id, CallerOf(target), ct)).Value;
        Assert.Empty(after.YourReveals);
        Assert.All(after.PlayedPlotCards, x => Assert.True(x.IsConfirmed));
    }

    [Fact]
    public async Task APlayedCardWithoutATarget_IsLoggedAndShowsNobodyAnIdentityCard()
    {
        var ct = CancellationToken.None;
        var callers = await OpenGameAndJoinAsync(5);
        await _roundService.StartAsync(_id, Host, ct);
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

        var view = (await _lobbyService.GetAsync(_id, callers.First(x => x.PlayerId == holder.PlayerId), ct)).Value;

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
        await _roundService.StartAsync(_id, Host, ct);
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

        var before = (await _lobbyService.GetAsync(_id, bystander, ct)).Value;
        players[1].MissionCard = false;
        var after = (await _lobbyService.GetAsync(_id, bystander, ct)).Value;

        Assert.Equal(players[1].PlayerId, before.OpenMissionPlayerId);
        Assert.Null(before.OpenMissionCard);
        Assert.Equal(false, after.OpenMissionCard);
        Assert.Null(after.YourMissionCard);
    }
}
