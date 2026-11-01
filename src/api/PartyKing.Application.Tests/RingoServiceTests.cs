using PartyKing.Application.Admin.Services;
using PartyKing.Application.Games.Services.Ringo;
using PartyKing.Application.Games.Services;
using PartyKing.Application.Sessions.Services;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Infrastructure.Results;

namespace PartyKing.Application.Tests;

public class RingoServiceTests
{
    private const string HostId = "host-1";

    private readonly FakeSessionsRepository _sessions = new();
    private readonly FakeRingoGamesRepository _games = new();
    private readonly FakeSessionMusicNotifier _notifier = new();
    private readonly RingoService _service;
    private readonly Guid _id;

    public RingoServiceTests()
    {
        var clock = new FakeClock();
        var sessionAuthorizer = new SessionAuthorizer(_sessions);
        _service = new RingoService(
            new RingoGameLobby(sessionAuthorizer, _sessions, _games, clock, new RingoLobbyNotifier(_notifier)),
            _games,
            clock,
            _notifier);
        _id = _sessions.AddSession(HostId, new PasswordHasher().Hash("secret")).Id;
    }

    [Fact]
    public async Task Guests_AreForbiddenOnEveryAction()
    {
        var ct = CancellationToken.None;

        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.GetAsync(_id, new GameCaller("guest", false), ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.StartAsync(_id, new GameCaller("guest", false), ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.FlipAsync(_id, 1, new GameCaller("guest", false), ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.EndAsync(_id, new GameCaller("guest", false), ct)).FirstError.Type);
    }

    [Fact]
    public async Task ASessionWithoutAGame_IsNotFound()
    {
        var result = await _service.GetAsync(_id, new GameCaller(HostId, false), CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
    }

    [Theory]
    [InlineData(HostId, false)]
    [InlineData(null, true)]
    public async Task TheHostAndAdmins_StartAGameWithNothingFlipped(string? profileId, bool isAdmin)
    {
        var started = await _service.StartAsync(_id, new GameCaller(profileId, isAdmin), CancellationToken.None);
        var resumed = await _service.GetAsync(_id, new GameCaller(profileId, isAdmin), CancellationToken.None);

        Assert.Empty(started.Value.Flips);
        Assert.Null(started.Value.PendingCardId);
        Assert.Equal(started.Value.Seed, resumed.Value.Seed);
    }

    [Fact]
    public async Task StartingASecondGame_IsAConflict()
    {
        await _service.StartAsync(_id, new GameCaller(HostId, false), CancellationToken.None);

        var result = await _service.StartAsync(_id, new GameCaller(HostId, false), CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.Conflict, result.FirstError.Type);
    }

    [Fact]
    public async Task Flipping_KeepsTheFlipOrderAndIgnoresRepeats()
    {
        await _service.StartAsync(_id, new GameCaller(HostId, false), CancellationToken.None);

        await _service.FlipAsync(_id, 30, new GameCaller(HostId, false), CancellationToken.None);
        await _service.ContinueAsync(_id, new GameCaller(HostId, false), CancellationToken.None);
        await _service.FlipAsync(_id, 4, new GameCaller(HostId, false), CancellationToken.None);
        await _service.ContinueAsync(_id, new GameCaller(HostId, false), CancellationToken.None);
        var result = await _service.FlipAsync(_id, 30, new GameCaller(HostId, false), CancellationToken.None);

        Assert.Equal([30, 4], result.Value.Flips.Select(x => x.CardId));
        Assert.Null(result.Value.PendingCardId);
    }

    [Theory]
    [InlineData(HostId, false, "guest")]
    [InlineData(null, true, "admin")]
    public async Task Flipping_RecordsWhoFlippedAndWhen(string? profileId, bool isAdmin, string expectedName)
    {
        await _service.StartAsync(_id, new GameCaller(profileId, isAdmin), CancellationToken.None);

        var result = await _service.FlipAsync(_id, 12, new GameCaller(profileId, isAdmin), CancellationToken.None);

        var flip = Assert.Single(result.Value.Flips);
        Assert.Equal(12, flip.CardId);
        Assert.Equal(expectedName, flip.FlippedBy);
        Assert.Equal(new FakeClock().UtcNow, flip.FlippedAt);
    }

    [Fact]
    public async Task AFlippedCard_StaysPendingUntilContinued()
    {
        await _service.StartAsync(_id, new GameCaller(HostId, false), CancellationToken.None);

        var flipped = await _service.FlipAsync(_id, 12, new GameCaller(HostId, false), CancellationToken.None);
        var blocked = await _service.FlipAsync(_id, 3, new GameCaller(HostId, false), CancellationToken.None);
        var repeated = await _service.FlipAsync(_id, 12, new GameCaller(HostId, false), CancellationToken.None);
        var continued = await _service.ContinueAsync(_id, new GameCaller(HostId, false), CancellationToken.None);

        Assert.Equal(12, flipped.Value.PendingCardId);
        Assert.Equal(ErrorOr.ErrorType.Conflict, blocked.FirstError.Type);
        Assert.Equal(12, repeated.Value.PendingCardId);
        Assert.Null(continued.Value.PendingCardId);
        Assert.Single(continued.Value.Flips);
    }

    [Fact]
    public async Task TheFourthKing_EndsTheGame()
    {
        var ct = CancellationToken.None;
        await _service.StartAsync(_id, new GameCaller(HostId, false), ct);

        // Kings are the cards 11, 24, 37 and 50
        foreach (var king in new[] { 11, 24, 37 })
        {
            await _service.FlipAsync(_id, king, new GameCaller(HostId, false), ct);
            await _service.ContinueAsync(_id, new GameCaller(HostId, false), ct);
        }

        var fourth = await _service.FlipAsync(_id, 50, new GameCaller(HostId, false), ct);
        await _service.ContinueAsync(_id, new GameCaller(HostId, false), ct);
        var after = await _service.FlipAsync(_id, 3, new GameCaller(HostId, false), ct);

        Assert.False(fourth.IsError);
        Assert.Equal(ErrorOr.ErrorType.Conflict, after.FirstError.Type);
        Assert.Equal("Ringo.GameOver", after.FirstError.Code);
    }

    [Fact]
    public async Task Continuing_IsIdempotentAndNeedsAGameAndAManager()
    {
        var ct = CancellationToken.None;

        Assert.Equal(ErrorOr.ErrorType.NotFound, (await _service.ContinueAsync(_id, new GameCaller(HostId, false), ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.ContinueAsync(_id, new GameCaller("guest", false), ct)).FirstError.Type);

        await _service.StartAsync(_id, new GameCaller(HostId, false), ct);
        Assert.False((await _service.ContinueAsync(_id, new GameCaller(HostId, false), ct)).IsError);
    }

    [Fact]
    public async Task ContinuingWithNoCardPending_PushesNothing()
    {
        var ct = CancellationToken.None;
        await _service.StartAsync(_id, new GameCaller(HostId, false), ct);
        var pushes = _notifier.RingoChanges.Count;

        Assert.False((await _service.ContinueAsync(_id, new GameCaller(HostId, false), ct)).IsError);

        Assert.Equal(pushes, _notifier.RingoChanges.Count);
    }

    [Fact]
    public async Task EveryChange_IsPushedToTheSession()
    {
        var ct = CancellationToken.None;

        await _service.StartAsync(_id, new GameCaller(HostId, false), ct);
        await _service.FlipAsync(_id, 5, new GameCaller(HostId, false), ct);
        await _service.FlipAsync(_id, 5, new GameCaller(HostId, false), ct); // repeat: nothing changed
        await _service.FlipAsync(_id, 6, new GameCaller(HostId, false), ct); // blocked: nothing changed
        await _service.ContinueAsync(_id, new GameCaller(HostId, false), ct);
        await _service.EndAsync(_id, new GameCaller(HostId, false), ct);

        Assert.Equal(Enumerable.Repeat(_id, 4), _notifier.RingoChanges);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(52)]
    public async Task FlippingAnUnknownCard_IsRejected(int cardId)
    {
        await _service.StartAsync(_id, new GameCaller(HostId, false), CancellationToken.None);

        var result = await _service.FlipAsync(_id, cardId, new GameCaller(HostId, false), CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public async Task FlippingWithoutAGame_IsNotFound()
    {
        var result = await _service.FlipAsync(_id, 3, new GameCaller(HostId, false), CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task EndingTheGame_AllowsStartingANewOne()
    {
        await _service.StartAsync(_id, new GameCaller(HostId, false), CancellationToken.None);
        await _service.FlipAsync(_id, 3, new GameCaller(HostId, false), CancellationToken.None);

        var ended = await _service.EndAsync(_id, new GameCaller(HostId, false), CancellationToken.None);
        var restarted = await _service.StartAsync(_id, new GameCaller(HostId, false), CancellationToken.None);

        Assert.False(ended.IsError);
        Assert.Empty(restarted.Value.Flips);
    }

    [Fact]
    public async Task EndingWithoutAGame_IsNotFound()
    {
        var result = await _service.EndAsync(_id, new GameCaller(HostId, false), CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
    }

    private static readonly GameCaller Admin = new(null, true);
    private static readonly GameCaller Host = new(HostId, false);

    private async Task StartOpenGameAsync()
    {
        await _service.StartAsync(_id, Admin, CancellationToken.None);
        await _service.OpenAsync(_id, Admin, CancellationToken.None);
    }

    private async Task<GameCaller> JoinAsync(string name)
    {
        var joined = await _service.JoinAsync(_id, name, new GameCaller("guest", false), CancellationToken.None);

        return new GameCaller("guest", false, joined.Value);
    }

    [Fact]
    public async Task Opening_IsForAdminsOnlyAndPushedOnce()
    {
        var ct = CancellationToken.None;
        await _service.StartAsync(_id, Admin, ct);

        var byHost = await _service.OpenAsync(_id, Host, ct);
        var first = await _service.OpenAsync(_id, Admin, ct);
        var again = await _service.OpenAsync(_id, Admin, ct);

        Assert.Equal(ErrorOr.ErrorType.Forbidden, byHost.FirstError.Type);
        Assert.True(first.Value.IsOpen);
        Assert.True(again.Value.IsOpen);
        Assert.Equal([_id], _notifier.RingoOpens);
    }

    [Fact]
    public async Task OpeningWithoutAGame_IsNotFound()
    {
        var result = await _service.OpenAsync(_id, Admin, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task Joining_NeedsAnOpenGame()
    {
        var ct = CancellationToken.None;
        var guest = new GameCaller("guest", false);
        Assert.Equal(ErrorOr.ErrorType.NotFound, (await _service.JoinAsync(_id, "Ola", guest, ct)).FirstError.Type);

        await _service.StartAsync(_id, Admin, ct);
        var closed = await _service.JoinAsync(_id, "Ola", guest, ct);

        Assert.Equal("Ringo.NotOpen", closed.FirstError.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A name that is much too long to fit")]
    [InlineData("Adam (host)")]
    [InlineData("adam (HOST)")]
    public async Task Joining_RejectsBadNames(string name)
    {
        await StartOpenGameAsync();

        var result = await _service.JoinAsync(_id, name, new GameCaller("guest", false), CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public async Task Joining_NamesAreUniqueIgnoringCaseAndSpaces()
    {
        await StartOpenGameAsync();
        await JoinAsync("Ola");

        var duplicate = await _service.JoinAsync(_id, "  OLA ", new GameCaller("guest", false), CancellationToken.None);

        Assert.Equal("Ringo.NameTaken", duplicate.FirstError.Code);
    }

    [Fact]
    public async Task JoiningAgain_KeepsTheSamePlayer()
    {
        await StartOpenGameAsync();
        var ola = await JoinAsync("Ola");

        var again = await _service.JoinAsync(_id, "Other", ola, CancellationToken.None);
        var game = await _service.GetAsync(_id, Admin, CancellationToken.None);

        Assert.Equal(ola.PlayerId, again.Value);
        Assert.Single(game.Value.Players);
    }

    [Fact]
    public async Task TheHost_JoinsWithTheirProfileNameAndASuffix()
    {
        await StartOpenGameAsync();

        var joined = await _service.JoinAsync(_id, "ignored", Host, CancellationToken.None);
        var game = await _service.GetAsync(_id, Admin, CancellationToken.None);

        var player = Assert.Single(game.Value.Players);
        Assert.Equal(joined.Value, player.Id);
        Assert.Equal("Host (host)", player.Name);
    }

    [Fact]
    public async Task AnAdmin_JoinsUnderATypedNameOrAsAdmin()
    {
        await StartOpenGameAsync();

        var named = await _service.JoinAsync(_id, "  Kasia ", Admin, CancellationToken.None);
        var skipped = await _service.JoinAsync(_id, null, Admin, CancellationToken.None);
        var game = await _service.GetAsync(_id, Admin, CancellationToken.None);

        Assert.False(named.IsError);
        Assert.False(skipped.IsError);
        Assert.Equal(["Kasia", "admin"], game.Value.Players.Select(x => x.Name));
    }

    [Fact]
    public async Task AnAdmin_StillNeedsAValidTypedName()
    {
        await StartOpenGameAsync();

        var result = await _service.JoinAsync(_id, "Adam (host)", Admin, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.Validation, result.FirstError.Type);
    }

    [Theory]
    [InlineData("Adam Strachanowski", "Adam")]
    [InlineData("  Adam   Strachanowski ", "Adam")]
    [InlineData("Madonna", "Madonna")]
    [InlineData("", "Host")]
    [InlineData(null, "Host")]
    public void TheHostName_IsTheFirstWordOfTheProfileName(string? displayName, string expected)
    {
        Assert.Equal(expected, LobbyNames.HostName(displayName));
    }

    [Fact]
    public async Task OnlyPlayersAndManagersSeeTheGame()
    {
        var ct = CancellationToken.None;
        await StartOpenGameAsync();
        var ola = await JoinAsync("Ola");
        var stranger = new GameCaller("stranger", false, Guid.NewGuid());

        var seenByPlayer = await _service.GetAsync(_id, ola, ct);
        var seenByStranger = await _service.GetAsync(_id, stranger, ct);

        Assert.Equal(ola.PlayerId, seenByPlayer.Value.YourPlayerId);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, seenByStranger.FirstError.Type);
        Assert.Null((await _service.GetAsync(_id, Admin, ct)).Value.YourPlayerId);
    }

    [Fact]
    public async Task TheLobby_ShowsWhetherTheCallerJoined()
    {
        var ct = CancellationToken.None;
        await StartOpenGameAsync();
        var ola = await JoinAsync("Ola");

        var joined = await _service.GetLobbyAsync(_id, ola, ct);
        var outsider = await _service.GetLobbyAsync(_id, new GameCaller("guest", false), ct);

        Assert.True(joined.Value.IsOpen);
        Assert.True(joined.Value.Joined);
        Assert.False(outsider.Value.Joined);
    }

    [Fact]
    public async Task Players_FlipOnlyOnTheirTurnUnderTheirName()
    {
        var ct = CancellationToken.None;
        await StartOpenGameAsync();
        var ola = await JoinAsync("Ola");
        var ewa = await JoinAsync("Ewa");

        var outOfTurn = await _service.FlipAsync(_id, 5, ewa, ct);
        var onTurn = await _service.FlipAsync(_id, 5, ola, ct);

        Assert.Equal("Ringo.NotYourTurn", outOfTurn.FirstError.Code);
        Assert.Equal("Ola", Assert.Single(onTurn.Value.Flips).FlippedBy);
    }

    [Fact]
    public async Task Continuing_PassesTheTurnRoundTheTable()
    {
        var ct = CancellationToken.None;
        await StartOpenGameAsync();
        var ola = await JoinAsync("Ola");
        var ewa = await JoinAsync("Ewa");

        await _service.FlipAsync(_id, 5, ola, ct);
        var notYet = await _service.ContinueAsync(_id, ewa, ct);
        var afterOla = await _service.ContinueAsync(_id, ola, ct);
        await _service.FlipAsync(_id, 6, ewa, ct);
        var afterEwa = await _service.ContinueAsync(_id, ewa, ct);

        Assert.Equal("Ringo.NotYourTurn", notYet.FirstError.Code);
        Assert.Equal(ewa.PlayerId, afterOla.Value.CurrentPlayerId);
        Assert.Equal(ola.PlayerId, afterEwa.Value.CurrentPlayerId);
    }

    [Fact]
    public async Task Managers_FlipAndContinueOutOfTurnForThePlayerWhoseTurnItIs()
    {
        var ct = CancellationToken.None;
        await StartOpenGameAsync();
        var ola = await JoinAsync("Ola");
        var ewa = await JoinAsync("Ewa");

        var flipped = await _service.FlipAsync(_id, 5, Admin, ct);
        var continued = await _service.ContinueAsync(_id, Host, ct);

        Assert.Equal("Ola", Assert.Single(flipped.Value.Flips).FlippedBy);
        Assert.Equal(ewa.PlayerId, continued.Value.CurrentPlayerId);
        Assert.NotEqual(ola.PlayerId, continued.Value.CurrentPlayerId);
    }

    [Fact]
    public async Task Reordering_ChangesWhoIsNextAndNeedsExactlyThePlayers()
    {
        var ct = CancellationToken.None;
        await StartOpenGameAsync();
        var ola = await JoinAsync("Ola");
        var ewa = await JoinAsync("Ewa");
        var ala = await JoinAsync("Ala");

        var reordered = await _service.ReorderAsync(
            _id, [ala.PlayerId!.Value, ola.PlayerId!.Value, ewa.PlayerId!.Value], Admin, ct);
        var missing = await _service.ReorderAsync(_id, [ala.PlayerId.Value], Admin, ct);
        var forbidden = await _service.ReorderAsync(_id, [], new GameCaller("guest", false), ct);

        Assert.Equal(["Ala", "Ola", "Ewa"], reordered.Value.Players.Select(x => x.Name));
        Assert.Equal("Ringo.OrderMismatch", missing.FirstError.Code);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, forbidden.FirstError.Type);
    }

    [Fact]
    public async Task Kicking_ARemovedPlayerLosesAccessAndTheirTurnMovesOn()
    {
        var ct = CancellationToken.None;
        await StartOpenGameAsync();
        var ola = await JoinAsync("Ola");
        var ewa = await JoinAsync("Ewa");

        var kicked = await _service.KickAsync(_id, ola.PlayerId!.Value, Host, ct);
        var denied = await _service.GetAsync(_id, ola, ct);
        var forbidden = await _service.KickAsync(_id, ewa.PlayerId!.Value, ola, ct);
        var unknown = await _service.KickAsync(_id, Guid.NewGuid(), Admin, ct);

        Assert.Equal(["Ewa"], kicked.Value.Players.Select(x => x.Name));
        Assert.Equal(ewa.PlayerId, kicked.Value.CurrentPlayerId);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, denied.FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, forbidden.FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.NotFound, unknown.FirstError.Type);
    }

    [Fact]
    public async Task Kicking_ThePlayerWithAPendingCard_StillPassesTheTurnToTheFollowerOnContinue()
    {
        var ct = CancellationToken.None;
        await StartOpenGameAsync();
        var ola = await JoinAsync("Ola");
        var ewa = await JoinAsync("Ewa");
        var ala = await JoinAsync("Ala");
        await _service.FlipAsync(_id, 5, ewa, ct); // not Ewa's turn, ignored
        await _service.FlipAsync(_id, 5, ola, ct);
        await _service.ContinueAsync(_id, ola, ct);
        await _service.FlipAsync(_id, 6, ewa, ct);

        await _service.KickAsync(_id, ewa.PlayerId!.Value, Admin, ct);
        var continued = await _service.ContinueAsync(_id, Admin, ct);

        Assert.Equal(ala.PlayerId, continued.Value.CurrentPlayerId);
    }

    [Fact]
    public async Task APlayerWhoIsNotManager_CannotStartOpenOrEnd()
    {
        var ct = CancellationToken.None;
        await StartOpenGameAsync();
        var ola = await JoinAsync("Ola");

        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.StartAsync(_id, ola, ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.OpenAsync(_id, ola, ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.EndAsync(_id, ola, ct)).FirstError.Type);
    }

    private sealed class FakeRingoGamesRepository : IRingoGamesRepository
    {
        private readonly Dictionary<Guid, RingoGame> _games = new();

        public Task<RingoGame?> GetAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(_games.GetValueOrDefault(sessionId));

        public Task<bool> TryCreateAsync(RingoGame game, CancellationToken cancellationToken) =>
            Task.FromResult(_games.TryAdd(game.SessionId, game));

        public Task<TResult> UpdateAsync<TResult>(
            Guid sessionId,
            string operation,
            TResult noGame,
            Func<RingoGame, TResult> action,
            CancellationToken cancellationToken)
            where TResult : IGameUpdateResult
        {
            return Task.FromResult(_games.TryGetValue(sessionId, out var game) ? action(game) : noGame);
        }

        public Task<RingoGame?> OpenAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            var game = _games.GetValueOrDefault(sessionId);
            if (game is not null)
            {
                game.IsOpen = true;
            }

            return Task.FromResult(game);
        }


        public Task<LobbyResult<RingoGame>> JoinAsync(
            Guid sessionId, RingoPlayer player, CancellationToken cancellationToken)
        {
            if (!_games.TryGetValue(sessionId, out var game))
            {
                return Task.FromResult(new LobbyResult<RingoGame>(LobbyStatus.NoGame, null));
            }

            if (!game.IsOpen)
            {
                return Task.FromResult(new LobbyResult<RingoGame>(LobbyStatus.NotOpen, game));
            }

            if (game.Players.Any(x => x.NormalizedName == player.NormalizedName))
            {
                return Task.FromResult(new LobbyResult<RingoGame>(LobbyStatus.NameTaken, game));
            }

            player.SessionId = sessionId;
            player.Order = game.Players.Count;
            game.Players.Add(player);
            game.CurrentPlayerId ??= player.PlayerId;

            return Task.FromResult(new LobbyResult<RingoGame>(LobbyStatus.Done, game));
        }

        public Task<LobbyResult<RingoGame>> KickAsync(Guid sessionId, Guid playerId, CancellationToken cancellationToken)
        {
            if (!_games.TryGetValue(sessionId, out var game))
            {
                return Task.FromResult(new LobbyResult<RingoGame>(LobbyStatus.NoGame, null));
            }

            var kicked = game.Players.FirstOrDefault(x => x.PlayerId == playerId);
            if (kicked is null)
            {
                return Task.FromResult(new LobbyResult<RingoGame>(LobbyStatus.NoPlayer, game));
            }

            var wasCurrent = RingoTurns.Current(game)?.PlayerId == playerId;
            var next = RingoTurns.AfterRemoving(game, kicked, wasCurrent);
            game.Players.Remove(kicked);
            var order = 0;
            foreach (var player in game.Players.OrderBy(x => x.Order))
            {
                player.Order = order++;
            }

            game.CurrentPlayerId = next?.PlayerId;

            return Task.FromResult(new LobbyResult<RingoGame>(LobbyStatus.Done, game));
        }

        public Task<LobbyResult<RingoGame>> ReorderAsync(
            Guid sessionId, IReadOnlyList<Guid> playerIds, CancellationToken cancellationToken)
        {
            if (!_games.TryGetValue(sessionId, out var game))
            {
                return Task.FromResult(new LobbyResult<RingoGame>(LobbyStatus.NoGame, null));
            }

            if (playerIds.Count != game.Players.Count
                || playerIds.Distinct().Count() != playerIds.Count
                || game.Players.Any(x => !playerIds.Contains(x.PlayerId)))
            {
                return Task.FromResult(new LobbyResult<RingoGame>(LobbyStatus.OrderMismatch, game));
            }

            foreach (var player in game.Players)
            {
                player.Order = playerIds.ToList().IndexOf(player.PlayerId);
            }

            return Task.FromResult(new LobbyResult<RingoGame>(LobbyStatus.Done, game));
        }

        public Task<bool> DeleteAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(_games.Remove(sessionId));
    }
}
