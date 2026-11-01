using PartyKing.Application.Games.Services;
using PartyKing.Contract.V1.Games.Resistance;
using PartyKing.Domain.Entities.Games;

namespace PartyKing.Application.Tests;

public class ResistanceLobbyServiceTests : ResistanceServiceTestBase
{
    [Fact]
    public async Task Guests_CannotCreateOpenKickReorderOrEnd()
    {
        var guest = new GameCaller("guest", false);
        var ct = CancellationToken.None;

        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _lobbyService.CreateAsync(_id, guest, ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _lobbyService.OpenAsync(_id, guest, ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _lobbyService.KickAsync(_id, Guid.NewGuid(), guest, ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _lobbyService.ReorderAsync(_id, [], guest, ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _lobbyService.EndAsync(_id, guest, ct)).FirstError.Type);
    }

    [Fact]
    public async Task Creating_SnapshotsTheConfiguredRules()
    {
        _rules.Rules.PlayerCounts[5] = new ResistanceCountRules(1, [2, 2, 2, 2, 2], [1, 1, 1, 1, 2]);

        var created = await _lobbyService.CreateAsync(_id, Host, CancellationToken.None);
        _rules.Rules = ResistanceRules.Default();

        var five = created.Value.Rules.PlayerCounts.Single(x => x.PlayerCount == 5);
        Assert.Equal(1, five.Spies);
        Assert.Equal([1, 1, 1, 1, 2], five.RequiredFails);
        Assert.Equal(1, (await _lobbyService.GetAsync(_id, Host, CancellationToken.None)).Value.Rules.PlayerCounts.Single(x => x.PlayerCount == 5).Spies);
    }

    [Fact]
    public async Task RefreshingRules_TakesTheCurrentConfigurationUntilTheGameIsOpen()
    {
        await _lobbyService.CreateAsync(_id, Host, CancellationToken.None);
        _rules.Rules.PlayerCounts[5] = new ResistanceCountRules(1, [2, 2, 2, 2, 2], [1, 1, 1, 1, 1]);

        var refreshed = await _lobbyService.RefreshRulesAsync(_id, Host, CancellationToken.None);

        Assert.Equal(1, refreshed.Value.Rules.PlayerCounts.Single(x => x.PlayerCount == 5).Spies);

        await _lobbyService.OpenAsync(_id, Admin, CancellationToken.None);
        var afterOpen = await _lobbyService.RefreshRulesAsync(_id, Host, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.Conflict, afterOpen.FirstError.Type);
    }

    [Fact]
    public async Task PlotCards_AreOffByDefaultAndSwitchedUntilTheGameStarts()
    {
        var created = await _lobbyService.CreateAsync(_id, Host, CancellationToken.None);
        Assert.False(created.Value.Rules.PlotCardsEnabled);

        var enabled = await _lobbyService.SetPlotCardsAsync(_id, true, Host, CancellationToken.None);
        Assert.True(enabled.Value.Rules.PlotCardsEnabled);

        await _lobbyService.OpenAsync(_id, Admin, CancellationToken.None);
        var afterOpen = await _lobbyService.SetPlotCardsAsync(_id, false, Host, CancellationToken.None);

        Assert.False(afterOpen.IsError);
        Assert.False((await _lobbyService.GetAsync(_id, Host, CancellationToken.None)).Value.Rules.PlotCardsEnabled);
    }

    [Fact]
    public async Task PlotCards_NeedAGameAndAManager()
    {
        var guest = new GameCaller("guest", false);

        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _lobbyService.SetPlotCardsAsync(_id, true, guest, CancellationToken.None)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.NotFound, (await _lobbyService.SetPlotCardsAsync(_id, true, Host, CancellationToken.None)).FirstError.Type);
    }

    [Fact]
    public async Task RefreshingRules_KeepsThePlotCardsSwitch()
    {
        await _lobbyService.CreateAsync(_id, Host, CancellationToken.None);
        await _lobbyService.SetPlotCardsAsync(_id, true, Host, CancellationToken.None);

        var refreshed = await _lobbyService.RefreshRulesAsync(_id, Host, CancellationToken.None);

        Assert.True(refreshed.Value.Rules.PlotCardsEnabled);
    }

    [Theory]
    [InlineData(5, 1)]
    [InlineData(6, 1)]
    [InlineData(7, 2)]
    [InlineData(8, 2)]
    [InlineData(9, 3)]
    [InlineData(10, 3)]
    public void PlotCardsPerRound_GrowsWithThePlayerCount(int players, int expected)
    {
        var rules = ResistanceRules.Default();

        Assert.Equal(expected, rules.PlotCardsPerRound(players));
        Assert.Equal(15, rules.PlotDeck.Values.Sum());
    }

    [Fact]
    public async Task RefreshingRules_NeedsAGameAndAManager()
    {
        var guest = new GameCaller("guest", false);

        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _lobbyService.RefreshRulesAsync(_id, guest, CancellationToken.None)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.NotFound, (await _lobbyService.RefreshRulesAsync(_id, Host, CancellationToken.None)).FirstError.Type);
    }

    [Fact]
    public async Task Creating_MakesAClosedGameAndOnlyOnce()
    {
        var created = await _lobbyService.CreateAsync(_id, Host, CancellationToken.None);
        var again = await _lobbyService.CreateAsync(_id, Admin, CancellationToken.None);

        Assert.False(created.IsError);
        Assert.False(created.Value.IsOpen);
        Assert.Empty(created.Value.Players);
        Assert.Equal("Resistance.AlreadyCreated", again.FirstError.Code);
        Assert.Equal([_id], _notifier.Changes);
    }

    [Fact]
    public async Task Opening_IsForAdminsOnlyAndPushedOnce()
    {
        await _lobbyService.CreateAsync(_id, Host, CancellationToken.None);

        var byHost = await _lobbyService.OpenAsync(_id, Host, CancellationToken.None);
        var first = await _lobbyService.OpenAsync(_id, Admin, CancellationToken.None);
        var second = await _lobbyService.OpenAsync(_id, Admin, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.Forbidden, byHost.FirstError.Type);
        Assert.True(first.Value.IsOpen);
        Assert.True(second.Value.IsOpen);
        Assert.Equal([_id], _notifier.Opens);
    }

    [Fact]
    public async Task OpeningWithoutAGame_IsNotFound()
    {
        var result = await _lobbyService.OpenAsync(_id, Admin, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task AClosedGame_IsHiddenFromGuestsButNotFromManagers()
    {
        var guest = new GameCaller("guest", false);
        await _lobbyService.CreateAsync(_id, Host, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, (await _lobbyService.GetAsync(_id, guest, CancellationToken.None)).FirstError.Type);
        Assert.False((await _lobbyService.GetAsync(_id, Host, CancellationToken.None)).IsError);
        Assert.False((await _lobbyService.GetAsync(_id, Admin, CancellationToken.None)).IsError);

        await _lobbyService.OpenAsync(_id, Admin, CancellationToken.None);

        Assert.False((await _lobbyService.GetAsync(_id, guest, CancellationToken.None)).IsError);
    }

    [Fact]
    public async Task TheLobby_ShowsWhetherTheGameIsOpenAndTheCallerJoined()
    {
        await OpenGameAsync();
        var player = await JoinAsync("Ola");

        var joined = await _lobbyService.GetLobbyAsync(_id, player, CancellationToken.None);
        var stranger = await _lobbyService.GetLobbyAsync(_id, new GameCaller("guest", false), CancellationToken.None);

        Assert.True(joined.Value.IsOpen);
        Assert.True(joined.Value.Joined);
        Assert.False(stranger.Value.Joined);
    }

    [Fact]
    public async Task Joining_NeedsAnOpenGame()
    {
        await _lobbyService.CreateAsync(_id, Host, CancellationToken.None);

        var result = await _lobbyService.JoinAsync(_id, "Ola", new GameCaller("guest", false), CancellationToken.None);

        Assert.Equal("Resistance.NotOpen", result.FirstError.Code);
    }

    [Fact]
    public async Task Joining_NamesAreUniqueIgnoringCaseAndSpaces()
    {
        await OpenGameAsync();
        await JoinAsync("Ola");

        var duplicate = await _lobbyService.JoinAsync(_id, "  OLA ", new GameCaller("guest", false), CancellationToken.None);

        Assert.Equal("Resistance.NameTaken", duplicate.FirstError.Code);
    }

    [Fact]
    public async Task Joining_StopsAtTenPlayers()
    {
        await OpenGameAsync();
        for (var i = 0; i < ResistanceGame.MaxPlayers; i++)
        {
            await JoinAsync($"Player {i}");
        }

        var eleventh = await _lobbyService.JoinAsync(_id, "Late", new GameCaller("guest", false), CancellationToken.None);
        var game = await _lobbyService.GetAsync(_id, Admin, CancellationToken.None);

        Assert.Equal("Resistance.Full", eleventh.FirstError.Code);
        Assert.Equal(ResistanceGame.MaxPlayers, game.Value.Players.Count);
    }

    [Fact]
    public async Task AGuestWhoJoined_WatchesTheGameAndIsMarkedAsAPlayer()
    {
        await OpenGameAsync();
        var player = await JoinAsync("Ola");

        var game = await _lobbyService.GetAsync(_id, player, CancellationToken.None);

        Assert.Equal(player.PlayerId, game.Value.YourPlayerId);
        Assert.Equal(["Ola"], game.Value.Players.Select(x => x.Name));
    }

    [Fact]
    public async Task AnObserver_HasNoPlayerId()
    {
        await OpenGameAsync();
        await JoinAsync("Ola");

        var game = await _lobbyService.GetAsync(_id, new GameCaller("guest", false), CancellationToken.None);

        Assert.Null(game.Value.YourPlayerId);
    }

    [Fact]
    public async Task TheHostAndAnAdmin_JoinUnderTheirOwnNames()
    {
        await OpenGameAsync();

        await _lobbyService.JoinAsync(_id, "ignored", Host, CancellationToken.None);
        await _lobbyService.JoinAsync(_id, null, Admin, CancellationToken.None);
        var game = await _lobbyService.GetAsync(_id, Admin, CancellationToken.None);

        Assert.Equal(["Host (host)", "admin"], game.Value.Players.Select(x => x.Name));
    }

    [Fact]
    public async Task Kicking_And_Reordering_AreForManagersAndPushed()
    {
        await OpenGameAsync();
        var ola = await JoinAsync("Ola");
        var ala = await JoinAsync("Ala");
        _notifier.Changes.Clear();

        var reordered = await _lobbyService.ReorderAsync(_id, [ala.PlayerId!.Value, ola.PlayerId!.Value], Host, CancellationToken.None);
        var mismatch = await _lobbyService.ReorderAsync(_id, [ala.PlayerId.Value], Host, CancellationToken.None);
        var kicked = await _lobbyService.KickAsync(_id, ala.PlayerId.Value, Admin, CancellationToken.None);
        var missing = await _lobbyService.KickAsync(_id, Guid.NewGuid(), Admin, CancellationToken.None);

        Assert.Equal(["Ala", "Ola"], reordered.Value.Players.Select(x => x.Name));
        Assert.Equal("Resistance.OrderMismatch", mismatch.FirstError.Code);
        Assert.Equal(["Ola"], kicked.Value.Players.Select(x => x.Name));
        Assert.Equal("Resistance.PlayerNotFound", missing.FirstError.Code);
        Assert.Equal(2, _notifier.Changes.Count);
    }

    [Fact]
    public async Task Ending_DeletesTheGameAndIsPushed()
    {
        await _lobbyService.CreateAsync(_id, Host, CancellationToken.None);
        _notifier.Changes.Clear();

        var ended = await _lobbyService.EndAsync(_id, Host, CancellationToken.None);
        var again = await _lobbyService.EndAsync(_id, Host, CancellationToken.None);

        Assert.False(ended.IsError);
        Assert.Equal(ErrorOr.ErrorType.NotFound, again.FirstError.Type);
        Assert.Equal([_id], _notifier.Changes);
    }

    [Fact]
    public async Task AfterStarting_PlayersCannotJoinBeKickedOrReordered()
    {
        await OpenGameAsync();
        var callers = await JoinPlayersAsync(5);
        await _roundService.StartAsync(_id, Host, CancellationToken.None);
        var ids = callers.Select(x => x.PlayerId!.Value).ToList();

        var join = await _lobbyService.JoinAsync(_id, "Late", new GameCaller("late", false), CancellationToken.None);
        var kick = await _lobbyService.KickAsync(_id, ids[0], Admin, CancellationToken.None);
        var reorder = await _lobbyService.ReorderAsync(_id, ids, Admin, CancellationToken.None);

        Assert.Equal("Resistance.Started", join.FirstError.Code);
        Assert.Equal("Resistance.Started", kick.FirstError.Code);
        Assert.Equal("Resistance.Started", reorder.FirstError.Code);
    }

    [Fact]
    public async Task TheLobby_ShowsWhetherTheGameStarted()
    {
        await OpenGameAsync();
        await JoinPlayersAsync(5);
        Assert.False((await _lobbyService.GetLobbyAsync(_id, Host, CancellationToken.None)).Value.IsStarted);

        await _roundService.StartAsync(_id, Host, CancellationToken.None);

        Assert.True((await _lobbyService.GetLobbyAsync(_id, Host, CancellationToken.None)).Value.IsStarted);
    }

    [Theory]
    [InlineData(ResistanceRoleDto.Spy, "\"Spy\"")]
    [InlineData(ResistanceRoleDto.Resistance, "\"Resistance\"")]
    public void TheRole_IsSerializedByNameSoTheUiCanCompareIt(ResistanceRoleDto role, string expected)
    {
        Assert.Equal(expected, global::System.Text.Json.JsonSerializer.Serialize(role));
    }
}
