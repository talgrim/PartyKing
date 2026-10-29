using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Admin.Services;
using PartyKing.Application.Games.Services;
using PartyKing.Application.Player.Services;
using PartyKing.Application.Realtime;
using PartyKing.Application.Sessions.Services;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Application.Tests;

public class ResistanceServiceTests
{
    private const string HostId = "host-1";

    private static readonly GameCaller Admin = new(null, true);
    private static readonly GameCaller Host = new(HostId, false);

    private readonly FakeSessionsRepository _sessions = new();
    private readonly FakeResistanceGamesRepository _games = new();
    private readonly FakeSessionMusicNotifier _notifier = new();
    private readonly ResistanceService _service;
    private readonly Guid _id;

    public ResistanceServiceTests()
    {
        var clock = new FakeClock();
        var sessionService = new SessionService(
            _sessions,
            new PasswordHasher(),
            new PlayerStateStore(clock),
            new FakeHostTokenProvider(),
            new FakeSlideshowService(),
            JoinTokens.Create(clock),
            clock,
            new NullSessionMusicNotifier(),
            new NullPlayerConnectionWatchdog(),
            NullLogger<SessionService>.Instance);
        _service = new ResistanceService(sessionService, _sessions, _games, clock, _notifier);
        _id = _sessions.AddSession(HostId, new PasswordHasher().Hash("secret")).Id;
    }

    [Fact]
    public async Task Guests_CannotCreateOpenKickReorderOrEnd()
    {
        var guest = new GameCaller("guest", false);
        var ct = CancellationToken.None;

        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.CreateAsync(_id, guest, ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.OpenAsync(_id, guest, ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.KickAsync(_id, Guid.NewGuid(), guest, ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.ReorderAsync(_id, [], guest, ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.EndAsync(_id, guest, ct)).FirstError.Type);
    }

    [Fact]
    public async Task Creating_MakesAClosedGameAndOnlyOnce()
    {
        var created = await _service.CreateAsync(_id, Host, CancellationToken.None);
        var again = await _service.CreateAsync(_id, Admin, CancellationToken.None);

        Assert.False(created.IsError);
        Assert.False(created.Value.IsOpen);
        Assert.Empty(created.Value.Players);
        Assert.Equal("Resistance.AlreadyCreated", again.FirstError.Code);
        Assert.Equal([_id], _notifier.ResistanceChanges);
    }

    [Fact]
    public async Task Opening_IsForAdminsOnlyAndPushedOnce()
    {
        await _service.CreateAsync(_id, Host, CancellationToken.None);

        var byHost = await _service.OpenAsync(_id, Host, CancellationToken.None);
        var first = await _service.OpenAsync(_id, Admin, CancellationToken.None);
        var second = await _service.OpenAsync(_id, Admin, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.Forbidden, byHost.FirstError.Type);
        Assert.True(first.Value.IsOpen);
        Assert.True(second.Value.IsOpen);
        Assert.Equal([_id], _notifier.ResistanceOpens);
    }

    [Fact]
    public async Task OpeningWithoutAGame_IsNotFound()
    {
        var result = await _service.OpenAsync(_id, Admin, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task AClosedGame_IsHiddenFromGuestsButNotFromManagers()
    {
        var guest = new GameCaller("guest", false);
        await _service.CreateAsync(_id, Host, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, (await _service.GetAsync(_id, guest, CancellationToken.None)).FirstError.Type);
        Assert.False((await _service.GetAsync(_id, Host, CancellationToken.None)).IsError);
        Assert.False((await _service.GetAsync(_id, Admin, CancellationToken.None)).IsError);

        await _service.OpenAsync(_id, Admin, CancellationToken.None);

        Assert.False((await _service.GetAsync(_id, guest, CancellationToken.None)).IsError);
    }

    [Fact]
    public async Task TheLobby_ShowsWhetherTheGameIsOpenAndTheCallerJoined()
    {
        await OpenGameAsync();
        var player = await JoinAsync("Ola");

        var joined = await _service.GetLobbyAsync(_id, player, CancellationToken.None);
        var stranger = await _service.GetLobbyAsync(_id, new GameCaller("guest", false), CancellationToken.None);

        Assert.True(joined.Value.IsOpen);
        Assert.True(joined.Value.Joined);
        Assert.False(stranger.Value.Joined);
    }

    [Fact]
    public async Task Joining_NeedsAnOpenGame()
    {
        await _service.CreateAsync(_id, Host, CancellationToken.None);

        var result = await _service.JoinAsync(_id, "Ola", new GameCaller("guest", false), CancellationToken.None);

        Assert.Equal("Resistance.NotOpen", result.FirstError.Code);
    }

    [Fact]
    public async Task Joining_NamesAreUniqueIgnoringCaseAndSpaces()
    {
        await OpenGameAsync();
        await JoinAsync("Ola");

        var duplicate = await _service.JoinAsync(_id, "  OLA ", new GameCaller("guest", false), CancellationToken.None);

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

        var eleventh = await _service.JoinAsync(_id, "Late", new GameCaller("guest", false), CancellationToken.None);
        var game = await _service.GetAsync(_id, Admin, CancellationToken.None);

        Assert.Equal("Resistance.Full", eleventh.FirstError.Code);
        Assert.Equal(ResistanceGame.MaxPlayers, game.Value.Players.Count);
    }

    [Fact]
    public async Task AGuestWhoJoined_WatchesTheGameAndIsMarkedAsAPlayer()
    {
        await OpenGameAsync();
        var player = await JoinAsync("Ola");

        var game = await _service.GetAsync(_id, player, CancellationToken.None);

        Assert.Equal(player.PlayerId, game.Value.YourPlayerId);
        Assert.Equal(["Ola"], game.Value.Players.Select(x => x.Name));
    }

    [Fact]
    public async Task AnObserver_HasNoPlayerId()
    {
        await OpenGameAsync();
        await JoinAsync("Ola");

        var game = await _service.GetAsync(_id, new GameCaller("guest", false), CancellationToken.None);

        Assert.Null(game.Value.YourPlayerId);
    }

    [Fact]
    public async Task TheHostAndAnAdmin_JoinUnderTheirOwnNames()
    {
        await OpenGameAsync();

        await _service.JoinAsync(_id, "ignored", Host, CancellationToken.None);
        await _service.JoinAsync(_id, null, Admin, CancellationToken.None);
        var game = await _service.GetAsync(_id, Admin, CancellationToken.None);

        Assert.Equal(["Host (host)", "admin"], game.Value.Players.Select(x => x.Name));
    }

    [Fact]
    public async Task Kicking_And_Reordering_AreForManagersAndPushed()
    {
        await OpenGameAsync();
        var ola = await JoinAsync("Ola");
        var ala = await JoinAsync("Ala");
        _notifier.ResistanceChanges.Clear();

        var reordered = await _service.ReorderAsync(_id, [ala.PlayerId!.Value, ola.PlayerId!.Value], Host, CancellationToken.None);
        var mismatch = await _service.ReorderAsync(_id, [ala.PlayerId.Value], Host, CancellationToken.None);
        var kicked = await _service.KickAsync(_id, ala.PlayerId.Value, Admin, CancellationToken.None);
        var missing = await _service.KickAsync(_id, Guid.NewGuid(), Admin, CancellationToken.None);

        Assert.Equal(["Ala", "Ola"], reordered.Value.Players.Select(x => x.Name));
        Assert.Equal("Resistance.OrderMismatch", mismatch.FirstError.Code);
        Assert.Equal(["Ola"], kicked.Value.Players.Select(x => x.Name));
        Assert.Equal("Resistance.PlayerNotFound", missing.FirstError.Code);
        Assert.Equal(2, _notifier.ResistanceChanges.Count);
    }

    [Fact]
    public async Task Ending_DeletesTheGameAndIsPushed()
    {
        await _service.CreateAsync(_id, Host, CancellationToken.None);
        _notifier.ResistanceChanges.Clear();

        var ended = await _service.EndAsync(_id, Host, CancellationToken.None);
        var again = await _service.EndAsync(_id, Host, CancellationToken.None);

        Assert.False(ended.IsError);
        Assert.Equal(ErrorOr.ErrorType.NotFound, again.FirstError.Type);
        Assert.Equal([_id], _notifier.ResistanceChanges);
    }

    private async Task OpenGameAsync()
    {
        await _service.CreateAsync(_id, Host, CancellationToken.None);
        await _service.OpenAsync(_id, Admin, CancellationToken.None);
    }

    private async Task<GameCaller> JoinAsync(string name)
    {
        var joined = await _service.JoinAsync(_id, name, new GameCaller("guest", false), CancellationToken.None);

        return new GameCaller("guest", false, joined.Value);
    }

    private sealed class FakeResistanceGamesRepository : IResistanceGamesRepository
    {
        private readonly Dictionary<Guid, ResistanceGame> _games = [];

        public Task<ResistanceGame?> GetAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(_games.GetValueOrDefault(sessionId));

        public Task<bool> TryCreateAsync(ResistanceGame game, CancellationToken cancellationToken) =>
            Task.FromResult(_games.TryAdd(game.SessionId, game));

        public Task<ResistanceGame?> OpenAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            var game = _games.GetValueOrDefault(sessionId);
            if (game is not null)
            {
                game.IsOpen = true;
            }

            return Task.FromResult(game);
        }

        public Task<LobbyResult<ResistanceGame>> JoinAsync(
            Guid sessionId, ResistancePlayer player, CancellationToken cancellationToken)
        {
            if (!_games.TryGetValue(sessionId, out var game))
            {
                return Result(LobbyStatus.NoGame, null);
            }

            if (!game.IsOpen)
            {
                return Result(LobbyStatus.NotOpen, game);
            }

            if (game.Players.Any(x => x.NormalizedName == player.NormalizedName))
            {
                return Result(LobbyStatus.NameTaken, game);
            }

            if (game.Players.Count >= ResistanceGame.MaxPlayers)
            {
                return Result(LobbyStatus.Full, game);
            }

            player.SessionId = sessionId;
            player.Order = game.Players.Count;
            game.Players.Add(player);

            return Result(LobbyStatus.Done, game);
        }

        public Task<LobbyResult<ResistanceGame>> KickAsync(
            Guid sessionId, Guid playerId, CancellationToken cancellationToken)
        {
            if (!_games.TryGetValue(sessionId, out var game))
            {
                return Result(LobbyStatus.NoGame, null);
            }

            var kicked = game.Players.FirstOrDefault(x => x.PlayerId == playerId);
            if (kicked is null)
            {
                return Result(LobbyStatus.NoPlayer, game);
            }

            game.Players.Remove(kicked);
            var order = 0;
            foreach (var player in game.Players.OrderBy(x => x.Order))
            {
                player.Order = order++;
            }

            return Result(LobbyStatus.Done, game);
        }

        public Task<LobbyResult<ResistanceGame>> ReorderAsync(
            Guid sessionId, IReadOnlyList<Guid> playerIds, CancellationToken cancellationToken)
        {
            if (!_games.TryGetValue(sessionId, out var game))
            {
                return Result(LobbyStatus.NoGame, null);
            }

            if (playerIds.Count != game.Players.Count || game.Players.Any(x => !playerIds.Contains(x.PlayerId)))
            {
                return Result(LobbyStatus.OrderMismatch, game);
            }

            foreach (var player in game.Players)
            {
                player.Order = playerIds.ToList().IndexOf(player.PlayerId);
            }

            return Result(LobbyStatus.Done, game);
        }

        public Task<bool> DeleteAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(_games.Remove(sessionId));

        private static Task<LobbyResult<ResistanceGame>> Result(LobbyStatus status, ResistanceGame? game) =>
            Task.FromResult(new LobbyResult<ResistanceGame>(status, game));
    }
}
