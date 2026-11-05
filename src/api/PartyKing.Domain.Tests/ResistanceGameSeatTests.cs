using PartyKing.Domain.Entities.Games.Resistance;
using PartyKing.Domain.Events.Games.Resistance;

namespace PartyKing.Domain.Tests;

/// <summary>Releasing a seat and taking it over (<see cref="ResistanceGame.SetSeatReleased"/>, <see cref="ResistanceGame.ClaimSeat"/>).</summary>
public class ResistanceGameSeatTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 20, 0, 0, TimeSpan.Zero);

    private static ResistanceGame StartedGame(bool started = true)
    {
        var game = new ResistanceGame { SessionId = Guid.NewGuid(), IsOpen = true, StartedAt = started ? Now : null };
        for (var i = 0; i < 5; i++)
        {
            game.Players.Add(ResistancePlayer.Create(Guid.NewGuid(), $"Player {i}", null, Now));
        }

        return game;
    }

    private static ResistancePlayer Host(ResistanceGame game)
    {
        var host = ResistancePlayer.Create(Guid.NewGuid(), "Host", "host-1", Now);
        game.Players.Add(host);

        return host;
    }

    private static ResistancePlayer Admin(ResistanceGame game)
    {
        var admin = ResistancePlayer.Create(Guid.NewGuid(), "Ola", null, Now, isAdminSeat: true);
        game.Players.Add(admin);

        return admin;
    }

    [Fact]
    public void Release_MarksAnOfflinePlayersSeat()
    {
        var game = StartedGame();
        var seat = game.Players[1];

        var outcome = game.SetSeatReleased(seat.PlayerId, released: true, playerOnline: false);

        Assert.Null(outcome.Failure);
        Assert.True(seat.IsReclaimable);
        Assert.Contains(new ResistanceSeatReleaseChanged(game.SessionId, seat.PlayerId, true), game.DrainEvents());
    }

    [Fact]
    public void Release_RefusesAnOnlinePlayer()
    {
        var game = StartedGame();
        var seat = game.Players[1];

        var outcome = game.SetSeatReleased(seat.PlayerId, released: true, playerOnline: true);

        Assert.Equal(ResistanceFailure.SeatOnline, outcome.Failure);
        Assert.False(seat.IsReclaimable);
        Assert.Empty(game.DrainEvents());
    }

    [Fact]
    public void TakingTheReleaseBack_WorksWhetherOrNotThePlayerIsOnline()
    {
        var game = StartedGame();
        var seat = game.Players[1];
        game.SetSeatReleased(seat.PlayerId, released: true, playerOnline: false);

        var outcome = game.SetSeatReleased(seat.PlayerId, released: false, playerOnline: true);

        Assert.Null(outcome.Failure);
        Assert.False(seat.IsReclaimable);
    }

    [Fact]
    public void Release_NeedsAStartedGame()
    {
        var game = StartedGame(started: false);

        var outcome = game.SetSeatReleased(game.Players[0].PlayerId, released: true, playerOnline: false);

        Assert.Equal(ResistanceFailure.NotStarted, outcome.Failure);
    }

    [Fact]
    public void Release_RefusesAnUnknownPlayer()
    {
        var game = StartedGame();

        var outcome = game.SetSeatReleased(Guid.NewGuid(), released: true, playerOnline: false);

        Assert.Equal(ResistanceFailure.UnknownSeat, outcome.Failure);
    }

    [Fact]
    public void Release_NeverHandsOutTheSeatOfTheHostOrAnAdmin()
    {
        var game = StartedGame();
        var host = Host(game);
        var admin = Admin(game);

        Assert.Equal(
            ResistanceFailure.ManagerSeat, game.SetSeatReleased(host.PlayerId, true, playerOnline: false).Failure);
        Assert.Equal(
            ResistanceFailure.ManagerSeat, game.SetSeatReleased(admin.PlayerId, true, playerOnline: false).Failure);
        Assert.False(host.IsReclaimable);
        Assert.False(admin.IsReclaimable);
    }

    [Fact]
    public void Claim_GivesTheSeatANewStampAndEndsTheRelease()
    {
        var game = StartedGame();
        var seat = game.Players[2];
        game.SetSeatReleased(seat.PlayerId, true, playerOnline: false);
        game.DrainEvents();
        var stamp = Guid.NewGuid();

        var outcome = game.ClaimSeat(seat.PlayerId, playerOnline: false, stamp);

        Assert.Null(outcome.Failure);
        Assert.Equal(stamp, seat.ClaimStamp);
        Assert.False(seat.IsReclaimable);
        Assert.Contains(new ResistanceSeatClaimed(game.SessionId, seat.PlayerId), game.DrainEvents());
    }

    [Fact]
    public void Claim_CutsTheOldDeviceOff()
    {
        var game = StartedGame();
        var seat = game.Players[2];
        game.SetSeatReleased(seat.PlayerId, true, playerOnline: false);
        var stamp = Guid.NewGuid();

        game.ClaimSeat(seat.PlayerId, playerOnline: false, stamp);

        Assert.False(seat.IsHeldBy(null));
        Assert.True(seat.IsHeldBy(stamp));
    }

    [Fact]
    public void Claim_RefusesASeatThatWasNotReleased()
    {
        var game = StartedGame();
        var seat = game.Players[2];

        var outcome = game.ClaimSeat(seat.PlayerId, playerOnline: false, Guid.NewGuid());

        Assert.Equal(ResistanceFailure.SeatNotReleased, outcome.Failure);
        Assert.Null(seat.ClaimStamp);
    }

    [Fact]
    public void Claim_RefusesAnUnknownSeatLikeAnUnreleasedOne()
    {
        var game = StartedGame();

        var outcome = game.ClaimSeat(Guid.NewGuid(), playerOnline: false, Guid.NewGuid());

        Assert.Equal(ResistanceFailure.SeatNotReleased, outcome.Failure);
    }

    [Fact]
    public void Claim_RefusesASeatWhoseOwnerCameBack_AndKeepsTheRelease()
    {
        var game = StartedGame();
        var seat = game.Players[2];
        game.SetSeatReleased(seat.PlayerId, true, playerOnline: false);

        var outcome = game.ClaimSeat(seat.PlayerId, playerOnline: true, Guid.NewGuid());

        Assert.Equal(ResistanceFailure.SeatOnline, outcome.Failure);
        Assert.True(seat.IsReclaimable);
        Assert.Null(seat.ClaimStamp);
    }

    [Fact]
    public void Claim_NeedsAStartedGame()
    {
        var game = StartedGame(started: false);

        var outcome = game.ClaimSeat(game.Players[0].PlayerId, playerOnline: false, Guid.NewGuid());

        Assert.Equal(ResistanceFailure.NotStarted, outcome.Failure);
    }

    [Fact]
    public void Claim_CanBeDoneAgainAfterTheHostReleasesTheNewSeatToo()
    {
        var game = StartedGame();
        var seat = game.Players[2];
        game.SetSeatReleased(seat.PlayerId, true, playerOnline: false);
        game.ClaimSeat(seat.PlayerId, playerOnline: false, Guid.NewGuid());
        game.SetSeatReleased(seat.PlayerId, true, playerOnline: false);
        var second = Guid.NewGuid();

        var outcome = game.ClaimSeat(seat.PlayerId, playerOnline: false, second);

        Assert.Null(outcome.Failure);
        Assert.Equal(second, seat.ClaimStamp);
    }

    [Fact]
    public void Restart_DropsTheReleaseOfEverySeat()
    {
        var game = StartedGame();
        game.Phase = ResistancePhase.GameOver;
        game.SetSeatReleased(game.Players[1].PlayerId, true, playerOnline: false);

        game.Restart();

        Assert.All(game.Players, x => Assert.False(x.IsReclaimable));
    }

    [Fact]
    public void ASeatThatNeverChangedHands_IsHeldByACookieWithoutAStamp()
    {
        var player = ResistancePlayer.Create(Guid.NewGuid(), "Ola", null, Now);

        Assert.True(player.IsHeldBy(null));
        Assert.False(player.IsHeldBy(Guid.NewGuid()));
    }
}
