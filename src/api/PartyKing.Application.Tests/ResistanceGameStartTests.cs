using PartyKing.Domain.Entities.Games;

namespace PartyKing.Application.Tests;

/// <summary>Starting and restarting a <see cref="ResistanceGame"/>, with no database.</summary>
public class ResistanceGameStartTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 20, 0, 0, TimeSpan.Zero);

    private static ResistanceGame NewGame(int playerCount, bool open = true)
    {
        var game = new ResistanceGame { SessionId = Guid.NewGuid(), IsOpen = open };
        for (var i = 0; i < playerCount; i++)
        {
            game.Players.Add(new ResistancePlayer { PlayerId = Guid.NewGuid(), Order = i });
        }

        return game;
    }

    [Fact]
    public void Start_OfAnOpenGameWithEnoughPlayers_DealsTheRolesAndPicksALeader()
    {
        var game = NewGame(6);

        var outcome = game.Start(Now, new Random(1));

        Assert.Null(outcome.Failure);
        Assert.True(outcome.Changed);
        Assert.Equal(Now, game.StartedAt);
        Assert.Equal(2, game.Players.Count(x => x.Role == ResistanceRole.Spy));
        Assert.Equal(4, game.Players.Count(x => x.Role == ResistanceRole.Resistance));
        Assert.Contains(game.Players, x => x.PlayerId == game.LeaderPlayerId);
    }

    [Fact]
    public void Start_WithTheSameRandom_DealsTheSameRoles()
    {
        var first = NewGame(7);
        var second = NewGame(7);
        for (var i = 0; i < 7; i++)
        {
            second.Players[i].PlayerId = first.Players[i].PlayerId;
        }

        first.Start(Now, new Random(5));
        second.Start(Now, new Random(5));

        Assert.Equal(first.Players.Select(x => x.Role), second.Players.Select(x => x.Role));
        Assert.Equal(first.LeaderPlayerId, second.LeaderPlayerId);
    }

    [Fact]
    public void Start_ChecksStartedBeforeOpenBeforeThePlayerCount()
    {
        var started = NewGame(3, open: false);
        started.StartedAt = Now;

        Assert.Equal(ResistanceFailure.AlreadyStarted, started.Start(Now, new Random(1)).Failure);
        Assert.Equal(ResistanceFailure.NotOpen, NewGame(3, open: false).Start(Now, new Random(1)).Failure);
        Assert.Equal(ResistanceFailure.NotEnoughPlayers, NewGame(4).Start(Now, new Random(1)).Failure);
    }

    [Fact]
    public void Start_WhenRefused_ChangesNothing()
    {
        var game = NewGame(4);

        var outcome = game.Start(Now, new Random(1));

        Assert.False(outcome.Changed);
        Assert.Null(game.StartedAt);
        Assert.All(game.Players, x => Assert.Null(x.Role));
        Assert.Null(game.LeaderPlayerId);
    }

    [Fact]
    public void Restart_OfAFinishedGame_ReturnsItToTheLobbyWithTheSamePlayers()
    {
        var game = NewGame(5);
        game.Start(Now, new Random(1));
        game.Phase = ResistancePhase.GameOver;
        game.Winner = ResistanceRole.Spy;
        game.RejectedTeams = 5;
        game.MissionNumber = 3;

        var outcome = game.Restart();

        Assert.Null(outcome.Failure);
        Assert.True(outcome.Changed);
        Assert.Null(game.StartedAt);
        Assert.Equal(ResistancePhase.SelectingTeam, game.Phase);
        Assert.Equal(1, game.MissionNumber);
        Assert.Equal(0, game.RejectedTeams);
        Assert.Null(game.Winner);
        Assert.Null(game.LeaderPlayerId);
        Assert.Equal(5, game.Players.Count);
        Assert.All(game.Players, x => Assert.Null(x.Role));
        Assert.True(game.IsOpen);
    }

    [Fact]
    public void Restart_BeforeTheStartOrWhileThePlayIsOn_IsRefusedAndChangesNothing()
    {
        var lobby = NewGame(5);
        var playing = NewGame(5);
        playing.Start(Now, new Random(1));

        var beforeStart = lobby.Restart();
        var whilePlaying = playing.Restart();

        Assert.Equal(ResistanceFailure.NotStarted, beforeStart.Failure);
        Assert.Equal(ResistanceFailure.NotOver, whilePlaying.Failure);
        Assert.False(whilePlaying.Changed);
        Assert.NotNull(playing.StartedAt);
        Assert.All(playing.Players, x => Assert.NotNull(x.Role));
    }
}
