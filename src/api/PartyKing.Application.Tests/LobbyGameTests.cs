using PartyKing.Domain.Entities.Games;

namespace PartyKing.Application.Tests;

/// <summary>The lobby rules every game shares (open, join, kick, reorder), with no database.</summary>
public class LobbyGameTests
{
    private static RingoPlayer Player(string name, string? profileId = null) =>
        new() { PlayerId = Guid.NewGuid(), Name = name, ProfileId = profileId };

    private static RingoGame OpenGame() => new() { SessionId = Guid.NewGuid(), IsOpen = true };

    private static ResistanceGame OpenResistance() => new() { SessionId = Guid.NewGuid(), IsOpen = true };

    [Fact]
    public void Open_ChangesOnlyAClosedGame()
    {
        var game = new RingoGame();

        Assert.True(game.Open());
        Assert.True(game.IsOpen);
        Assert.False(game.Open());
    }

    [Fact]
    public void Join_NeedsAnOpenGame()
    {
        var game = new RingoGame { SessionId = Guid.NewGuid() };
        var player = Player("Ola");

        Assert.Equal(LobbyStatus.NotOpen, game.Join(player));
        Assert.Empty(game.Players);
    }

    [Fact]
    public void Join_AppendsThePlayerWithTheSessionAndANormalizedName()
    {
        var game = OpenGame();
        var first = Player("Ola");
        var second = Player("Ala");

        Assert.Equal(LobbyStatus.Done, game.Join(first));
        Assert.Equal(LobbyStatus.Done, game.Join(second));

        Assert.Equal([0, 1], game.Players.Select(x => x.Order));
        Assert.All(game.Players, x => Assert.Equal(game.SessionId, x.SessionId));
        Assert.Equal("OLA", first.NormalizedName);
    }

    [Fact]
    public void Join_NamesAreUniqueIgnoringCase_AndTheHostSuffixCountsAsPartOfTheName()
    {
        var game = OpenGame();
        game.Join(Player("Ola"));

        Assert.Equal(LobbyStatus.NameTaken, game.Join(Player("OLA")));
        Assert.Equal(LobbyStatus.Done, game.Join(Player("Ola", profileId: "spotify-ola")));
        Assert.Equal("OLA (HOST)", game.Players[1].NormalizedName);
        Assert.Equal(2, game.Players.Count);
    }

    [Fact]
    public void Join_RingoGivesTheFirstPlayerTheTurn()
    {
        var game = OpenGame();
        var first = Player("Ola");

        game.Join(first);
        game.Join(Player("Ala"));

        Assert.Equal(first.PlayerId, game.CurrentPlayerId);
    }

    [Fact]
    public void Join_ResistanceStopsAtTenPlayersAndOnceStarted()
    {
        var game = OpenResistance();
        for (var i = 0; i < ResistanceGame.MaxPlayers; i++)
        {
            Assert.Equal(LobbyStatus.Done, game.Join(new ResistancePlayer { PlayerId = Guid.NewGuid(), Name = $"P{i}" }));
        }

        Assert.Equal(LobbyStatus.Full, game.Join(new ResistancePlayer { PlayerId = Guid.NewGuid(), Name = "Late" }));

        game.StartedAt = DateTimeOffset.UtcNow;
        Assert.Equal(LobbyStatus.Started, game.Join(new ResistancePlayer { PlayerId = Guid.NewGuid(), Name = "Later" }));
    }

    [Fact]
    public void Kick_RemovesThePlayerAndClosesTheGapInTheOrder()
    {
        var game = OpenGame();
        var players = new[] { Player("A"), Player("B"), Player("C") };
        foreach (var player in players)
        {
            game.Join(player);
        }

        var (status, kicked) = game.Kick(players[1].PlayerId);

        Assert.Equal(LobbyStatus.Done, status);
        Assert.Same(players[1], kicked);
        Assert.Equal(["A", "C"], game.Players.OrderBy(x => x.Order).Select(x => x.Name));
        Assert.Equal([0, 1], game.Players.OrderBy(x => x.Order).Select(x => x.Order));
    }

    [Fact]
    public void Kick_UnknownPlayerAndStartedGame_AreRefused()
    {
        var game = OpenResistance();
        var player = new ResistancePlayer { PlayerId = Guid.NewGuid(), Name = "A" };
        game.Join(player);

        Assert.Equal(LobbyStatus.NoPlayer, game.Kick(Guid.NewGuid()).Status);

        game.StartedAt = DateTimeOffset.UtcNow;
        Assert.Equal(LobbyStatus.Started, game.Kick(player.PlayerId).Status);
        Assert.Single(game.Players);
    }

    [Fact]
    public void Kick_RingoMovesTheTurnOnFromTheKickedPlayer()
    {
        var game = OpenGame();
        var first = Player("A");
        var second = Player("B");
        game.Join(first);
        game.Join(second);

        game.Kick(first.PlayerId);

        Assert.Equal(second.PlayerId, game.CurrentPlayerId);
    }

    [Fact]
    public void Reorder_SetsTheOrderOfExactlyTheGamesPlayers()
    {
        var game = OpenGame();
        var a = Player("A");
        var b = Player("B");
        game.Join(a);
        game.Join(b);

        Assert.Equal(LobbyStatus.Done, game.Reorder([b.PlayerId, a.PlayerId]));
        Assert.Equal((1, 0), (a.Order, b.Order));
    }

    [Fact]
    public void Reorder_RefusesAnyOtherSetOfIds()
    {
        var game = OpenGame();
        var a = Player("A");
        var b = Player("B");
        game.Join(a);
        game.Join(b);

        Assert.Equal(LobbyStatus.OrderMismatch, game.Reorder([a.PlayerId]));
        Assert.Equal(LobbyStatus.OrderMismatch, game.Reorder([a.PlayerId, a.PlayerId]));
        Assert.Equal(LobbyStatus.OrderMismatch, game.Reorder([a.PlayerId, Guid.NewGuid()]));
        Assert.Equal((0, 1), (a.Order, b.Order));
    }

    [Theory]
    [InlineData("", LobbyNameFailure.Empty)]
    [InlineData("abcdefghijklmnopqrstuvwxyz12345", LobbyNameFailure.TooLong)]
    [InlineData("Ola (host)", LobbyNameFailure.HostSuffix)]
    [InlineData("Ola (HOST)", LobbyNameFailure.HostSuffix)]
    public void Names_AreValidated(string name, LobbyNameFailure expected)
    {
        Assert.Equal(expected, LobbyNames.Validate(name));
    }

    [Fact]
    public void Names_OfTheRightLengthAreAccepted()
    {
        Assert.Null(LobbyNames.Validate("Ola"));
        Assert.Null(LobbyNames.Validate(new string('a', LobbyNames.MaxLength)));
    }
}
