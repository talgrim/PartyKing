using ErrorOr;
using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Games.Handlers;
using PartyKing.Application.Games.Services.Base;
using PartyKing.Application.Games.Services.Resistance;
using PartyKing.Application.Realtime;
using PartyKing.Application.Sessions.Services;
using PartyKing.Domain.Entities.Games.Base;
using PartyKing.Domain.Events.Games.Resistance;

namespace PartyKing.Application.Tests;

/// <summary>Returning to a started game: the host releases a seat, a guest claims it and the old device loses it.</summary>
public class ResistanceSeatServiceTests : ResistanceServiceTestBase
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly GamePresence _presence;
    private readonly ResistanceSeatService _seats;

    public ResistanceSeatServiceTests()
    {
        _presence = new GamePresence(_notifier, NullLogger<GamePresence>.Instance, TimeSpan.FromSeconds(30));
        var lobby = new ResistanceGameLobby(new SessionAuthorizer(_sessions), _sessions, _games, new FakeClock());
        _seats = new ResistanceSeatService(new ResistanceActionGate(lobby), _games, _presence);
    }

    private async Task<List<GameCaller>> StartedGameAsync(int count = 5)
    {
        var callers = await OpenGameAndJoinAsync(count);
        Assert.False((await _roundService.StartAsync(_id, Host, Ct)).IsError);

        return callers;
    }

    private async Task<GameSeat> ReleaseAndClaimAsync(GameCaller owner)
    {
        Assert.False((await _seats.SetReleasedAsync(_id, owner.PlayerId!.Value, true, Host, Ct)).IsError);
        var claimed = await _seats.ClaimAsync(_id, owner.PlayerId!.Value, Ct);
        Assert.False(claimed.IsError);

        return claimed.Value;
    }

    [Fact]
    public async Task TheHost_ReleasesTheSeatOfAnOfflinePlayer()
    {
        var callers = await StartedGameAsync();
        _notifier.LobbyChanges.Clear();

        var game = await _seats.SetReleasedAsync(_id, callers[0].PlayerId!.Value, true, Host, Ct);

        Assert.True(game.Value.Players.Single(x => x.Id == callers[0].PlayerId).IsSeatReleased);
        Assert.Equal([_id], _notifier.LobbyChanges);
    }

    [Fact]
    public async Task AnAdmin_MayReleaseASeatToo()
    {
        var callers = await StartedGameAsync();

        var game = await _seats.SetReleasedAsync(_id, callers[0].PlayerId!.Value, true, Admin, Ct);

        Assert.False(game.IsError);
    }

    [Fact]
    public async Task AGuest_CannotReleaseASeat_NotEvenTheirOwn()
    {
        var callers = await StartedGameAsync();

        var result = await _seats.SetReleasedAsync(_id, callers[0].PlayerId!.Value, true, callers[0], Ct);

        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
    }

    [Fact]
    public async Task ASeatWhosePlayerIsOnline_CannotBeReleased()
    {
        var callers = await StartedGameAsync();
        await _presence.ConnectAsync(GameKind.Resistance, _id, callers[0].PlayerId!.Value, "c1");

        var result = await _seats.SetReleasedAsync(_id, callers[0].PlayerId!.Value, true, Host, Ct);

        Assert.Equal("Resistance.SeatOnline", result.FirstError.Code);
    }

    [Fact]
    public async Task TheSeatOfTheHost_CannotBeReleased()
    {
        await OpenGameAsync();
        var hostSeat = (await _lobbyService.JoinAsync(_id, null, Host, Ct)).Value;
        await JoinPlayersAsync(4);
        await _roundService.StartAsync(_id, Host, Ct);

        var result = await _seats.SetReleasedAsync(_id, hostSeat.PlayerId, true, Admin, Ct);

        Assert.Equal("Resistance.ManagerSeat", result.FirstError.Code);
    }

    [Fact]
    public async Task TheSeatOfAnAdmin_CannotBeReleased_WhateverNameTheyTyped()
    {
        await OpenGameAsync();
        var adminSeat = (await _lobbyService.JoinAsync(_id, "Ola", Admin, Ct)).Value;
        await JoinPlayersAsync(4);
        await _roundService.StartAsync(_id, Host, Ct);

        var result = await _seats.SetReleasedAsync(_id, adminSeat.PlayerId, true, Host, Ct);

        Assert.Equal("Resistance.ManagerSeat", result.FirstError.Code);
    }

    [Fact]
    public async Task AGuestWhoTypedTheNameAdmin_HasAnOrdinarySeat()
    {
        await OpenGameAsync();
        var seat = (await _lobbyService.JoinAsync(_id, "admin", new GameCaller("guest", false), Ct)).Value;
        await JoinPlayersAsync(4);
        await _roundService.StartAsync(_id, Host, Ct);

        var result = await _seats.SetReleasedAsync(_id, seat.PlayerId, true, Host, Ct);

        Assert.False(result.IsError);
    }

    [Fact]
    public async Task ASeat_CannotBeReleasedBeforeTheStart()
    {
        var callers = await OpenGameAndJoinAsync(5);

        var result = await _seats.SetReleasedAsync(_id, callers[0].PlayerId!.Value, true, Host, Ct);

        Assert.Equal("Resistance.NotStarted", result.FirstError.Code);
    }

    [Fact]
    public async Task TheRelease_CanBeTakenBack()
    {
        var callers = await StartedGameAsync();
        await _seats.SetReleasedAsync(_id, callers[0].PlayerId!.Value, true, Host, Ct);

        await _seats.SetReleasedAsync(_id, callers[0].PlayerId!.Value, false, Host, Ct);

        Assert.Empty((await _seats.GetClaimableAsync(_id, Ct)).Value);
    }

    [Fact]
    public async Task ClaimableSeats_AreTheReleasedOnesWithNoDeviceConnected()
    {
        var callers = await StartedGameAsync();
        await _seats.SetReleasedAsync(_id, callers[0].PlayerId!.Value, true, Host, Ct);
        await _seats.SetReleasedAsync(_id, callers[1].PlayerId!.Value, true, Host, Ct);

        // the second player is back before anyone claimed the seat
        await _presence.ConnectAsync(GameKind.Resistance, _id, callers[1].PlayerId!.Value, "c1");
        var seats = await _seats.GetClaimableAsync(_id, Ct);

        var seat = Assert.Single(seats.Value);
        Assert.Equal(callers[0].PlayerId, seat.Id);
        Assert.Equal("Player 1", seat.Name);
    }

    [Fact]
    public async Task ASeatThatCameBackOnline_IsClaimableAgainWhenThePlayerDropsAgain()
    {
        var callers = await StartedGameAsync();
        await _seats.SetReleasedAsync(_id, callers[0].PlayerId!.Value, true, Host, Ct);
        await _presence.ConnectAsync(GameKind.Resistance, _id, callers[0].PlayerId!.Value, "c1");
        Assert.Empty((await _seats.GetClaimableAsync(_id, Ct)).Value);

        await _presence.ForgetAsync(GameKind.Resistance, _id, callers[0].PlayerId!.Value);

        Assert.Single((await _seats.GetClaimableAsync(_id, Ct)).Value);
    }

    [Fact]
    public async Task ClaimableSeats_AreEmptyBeforeTheStart_AndNotFoundWithoutAGame()
    {
        Assert.Equal(ErrorType.NotFound, (await _seats.GetClaimableAsync(_id, Ct)).FirstError.Type);

        await OpenGameAndJoinAsync(5);

        Assert.Empty((await _seats.GetClaimableAsync(_id, Ct)).Value);
    }

    [Fact]
    public async Task Claiming_GivesTheGuestTheSeatAndTakesItFromTheOldDevice()
    {
        var callers = await StartedGameAsync();
        var old = callers[0];

        var seat = await ReleaseAndClaimAsync(old);
        var oldView = await _lobbyService.GetAsync(_id, old, Ct);
        var newCaller = new GameCaller("guest", false, seat.PlayerId, seat.Stamp);
        var newView = await _lobbyService.GetAsync(_id, newCaller, Ct);

        Assert.Equal(old.PlayerId, seat.PlayerId);
        Assert.NotNull(seat.Stamp);
        Assert.Null(oldView.Value.YourPlayerId);
        Assert.Null(oldView.Value.YourRole);
        Assert.Equal(old.PlayerId, newView.Value.YourPlayerId);
        Assert.NotNull(newView.Value.YourRole);
    }

    [Fact]
    public async Task TheOldDevice_CannotVoteAnymore_TheNewOneCan()
    {
        var callers = await StartedGameAsync();
        var game = (await _lobbyService.GetAsync(_id, Host, Ct)).Value;
        var leader = callers.First(x => x.PlayerId == game.LeaderPlayerId);
        await _roundService.SelectTeamAsync(_id, game.Players.Take(game.RequiredTeamSize!.Value).Select(x => x.Id).ToList(), leader, Ct);
        var voter = callers.First(x => x != leader);
        var seat = await ReleaseAndClaimAsync(voter);

        var old = await _roundService.VoteAsync(_id, true, voter, Ct);
        var fresh = await _roundService.VoteAsync(_id, true, new GameCaller("guest", false, seat.PlayerId, seat.Stamp), Ct);

        Assert.Equal(ErrorType.Forbidden, old.FirstError.Type);
        Assert.False(fresh.IsError);
    }

    [Fact]
    public async Task TheLobby_CountsOnlyTheDeviceWithTheCurrentStamp_AsJoined()
    {
        var callers = await StartedGameAsync();
        var seat = await ReleaseAndClaimAsync(callers[0]);

        var old = await _lobbyService.GetLobbyAsync(_id, callers[0], Ct);
        var fresh = await _lobbyService.GetLobbyAsync(_id, new GameCaller("guest", false, seat.PlayerId, seat.Stamp), Ct);

        Assert.False(old.Value.Joined);
        Assert.True(fresh.Value.Joined);
    }

    [Fact]
    public async Task ClaimingASeat_IsPushedToTheLobby()
    {
        var callers = await StartedGameAsync();
        await _seats.SetReleasedAsync(_id, callers[0].PlayerId!.Value, true, Host, Ct);
        _notifier.LobbyChanges.Clear();

        await _seats.ClaimAsync(_id, callers[0].PlayerId!.Value, Ct);

        Assert.Equal([_id], _notifier.LobbyChanges);
    }

    [Fact]
    public async Task ASeatThatWasNotReleased_CannotBeClaimed()
    {
        var callers = await StartedGameAsync();

        var result = await _seats.ClaimAsync(_id, callers[0].PlayerId!.Value, Ct);

        Assert.Equal("Resistance.SeatNotReleased", result.FirstError.Code);
    }

    [Fact]
    public async Task ASeatWhosePlayerIsOnline_CannotBeClaimed_AndStaysReleased()
    {
        var callers = await StartedGameAsync();
        await _seats.SetReleasedAsync(_id, callers[0].PlayerId!.Value, true, Host, Ct);
        await _presence.ConnectAsync(GameKind.Resistance, _id, callers[0].PlayerId!.Value, "c1");

        var result = await _seats.ClaimAsync(_id, callers[0].PlayerId!.Value, Ct);

        Assert.Equal("Resistance.SeatOnline", result.FirstError.Code);
        Assert.True((await _games.GetAsync(_id, Ct))!.Players.Single(x => x.PlayerId == callers[0].PlayerId).IsReclaimable);
    }

    [Fact]
    public async Task ASeatClaimedOnce_CannotBeClaimedAgainWithoutANewRelease()
    {
        var callers = await StartedGameAsync();
        await ReleaseAndClaimAsync(callers[0]);

        var again = await _seats.ClaimAsync(_id, callers[0].PlayerId!.Value, Ct);

        Assert.Equal("Resistance.SeatNotReleased", again.FirstError.Code);
    }

    [Fact]
    public async Task ClaimingWithoutAGame_IsNotFound()
    {
        var result = await _seats.ClaimAsync(_id, Guid.NewGuid(), Ct);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task HoldsSeat_FollowsTheStamp()
    {
        var callers = await StartedGameAsync();
        var old = new GameSeat(callers[0].PlayerId!.Value, null);
        Assert.True(await _seats.HoldsSeatAsync(_id, old, Ct));

        var seat = await ReleaseAndClaimAsync(callers[0]);

        Assert.False(await _seats.HoldsSeatAsync(_id, old, Ct));
        Assert.True(await _seats.HoldsSeatAsync(_id, seat, Ct));
        Assert.False(await _seats.HoldsSeatAsync(_id, new GameSeat(Guid.NewGuid(), null), Ct));
        Assert.False(await _seats.HoldsSeatAsync(Guid.NewGuid(), seat, Ct));
    }

    [Fact]
    public async Task ASeatClaimedAway_DropsTheOldDevicesConnectionFromThePresence()
    {
        var callers = await StartedGameAsync();
        var player = callers[0].PlayerId!.Value;
        await _presence.ConnectAsync(GameKind.Resistance, _id, player, "old-tab");
        var handler = new ResistanceSeatClaimedHandler(_presence);

        await handler.HandleAsync(new ResistanceSeatClaimed(_id, player), Ct);

        Assert.DoesNotContain(player, _presence.OnlinePlayers(GameKind.Resistance, _id));
    }

    // Joining

    [Fact]
    public async Task TheHost_GetsTheirSeatBackInAStartedGame_WithoutTheCookie()
    {
        await OpenGameAsync();
        var first = (await _lobbyService.JoinAsync(_id, null, Host, Ct)).Value;
        await JoinPlayersAsync(4);
        await _roundService.StartAsync(_id, Host, Ct);

        var again = await _lobbyService.JoinAsync(_id, null, Host, Ct);

        Assert.False(again.IsError);
        Assert.Equal(first.PlayerId, again.Value.PlayerId);
    }

    [Fact]
    public async Task AGuestWithoutTheCookie_StillCannotJoinAStartedGame()
    {
        await StartedGameAsync();

        var join = await _lobbyService.JoinAsync(_id, "Late", new GameCaller("late", false), Ct);

        Assert.Equal("Resistance.Started", join.FirstError.Code);
    }

    [Fact]
    public async Task ADeviceThatLostItsSeat_CannotRejoinWithItsOldCookie()
    {
        var callers = await StartedGameAsync();
        await ReleaseAndClaimAsync(callers[0]);

        var join = await _lobbyService.JoinAsync(_id, "Player 1", callers[0], Ct);

        Assert.Equal("Resistance.Started", join.FirstError.Code);
    }

    [Fact]
    public async Task JoiningAgainWithTheCurrentStamp_ReturnsTheSameSeatAndStamp()
    {
        var callers = await StartedGameAsync();
        var seat = await ReleaseAndClaimAsync(callers[0]);

        var again = await _lobbyService.JoinAsync(_id, null, new GameCaller("guest", false, seat.PlayerId, seat.Stamp), Ct);

        Assert.Equal(seat, again.Value);
    }
}
