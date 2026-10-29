using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Admin.Services;
using PartyKing.Application.Games.Services;
using PartyKing.Application.Player.Services;
using PartyKing.Application.Realtime;
using PartyKing.Application.Sessions.Services;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Repositories;

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
        _service = new RingoService(sessionService, _games, clock, _notifier);
        _id = _sessions.AddSession(HostId, new PasswordHasher().Hash("secret")).Id;
    }

    [Fact]
    public async Task Guests_AreForbiddenOnEveryAction()
    {
        var ct = CancellationToken.None;

        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.GetAsync(_id, "guest", false, ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.StartAsync(_id, "guest", false, ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.FlipAsync(_id, 1, "guest", false, ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.EndAsync(_id, "guest", false, ct)).FirstError.Type);
    }

    [Fact]
    public async Task ASessionWithoutAGame_IsNotFound()
    {
        var result = await _service.GetAsync(_id, HostId, false, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
    }

    [Theory]
    [InlineData(HostId, false)]
    [InlineData(null, true)]
    public async Task TheHostAndAdmins_StartAGameWithNothingFlipped(string? profileId, bool isAdmin)
    {
        var started = await _service.StartAsync(_id, profileId, isAdmin, CancellationToken.None);
        var resumed = await _service.GetAsync(_id, profileId, isAdmin, CancellationToken.None);

        Assert.Empty(started.Value.Flips);
        Assert.Null(started.Value.PendingCardId);
        Assert.Equal(started.Value.Seed, resumed.Value.Seed);
    }

    [Fact]
    public async Task StartingASecondGame_IsAConflict()
    {
        await _service.StartAsync(_id, HostId, false, CancellationToken.None);

        var result = await _service.StartAsync(_id, HostId, false, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.Conflict, result.FirstError.Type);
    }

    [Fact]
    public async Task Flipping_KeepsTheFlipOrderAndIgnoresRepeats()
    {
        await _service.StartAsync(_id, HostId, false, CancellationToken.None);

        await _service.FlipAsync(_id, 30, HostId, false, CancellationToken.None);
        await _service.ContinueAsync(_id, HostId, false, CancellationToken.None);
        await _service.FlipAsync(_id, 4, HostId, false, CancellationToken.None);
        await _service.ContinueAsync(_id, HostId, false, CancellationToken.None);
        var result = await _service.FlipAsync(_id, 30, HostId, false, CancellationToken.None);

        Assert.Equal([30, 4], result.Value.Flips.Select(x => x.CardId));
        Assert.Null(result.Value.PendingCardId);
    }

    [Theory]
    [InlineData(HostId, false, "guest")]
    [InlineData(null, true, "admin")]
    public async Task Flipping_RecordsWhoFlippedAndWhen(string? profileId, bool isAdmin, string expectedName)
    {
        await _service.StartAsync(_id, profileId, isAdmin, CancellationToken.None);

        var result = await _service.FlipAsync(_id, 12, profileId, isAdmin, CancellationToken.None);

        var flip = Assert.Single(result.Value.Flips);
        Assert.Equal(12, flip.CardId);
        Assert.Equal(expectedName, flip.FlippedBy);
        Assert.Equal(new FakeClock().UtcNow, flip.FlippedAt);
    }

    [Fact]
    public async Task AFlippedCard_StaysPendingUntilContinued()
    {
        await _service.StartAsync(_id, HostId, false, CancellationToken.None);

        var flipped = await _service.FlipAsync(_id, 12, HostId, false, CancellationToken.None);
        var blocked = await _service.FlipAsync(_id, 3, HostId, false, CancellationToken.None);
        var repeated = await _service.FlipAsync(_id, 12, HostId, false, CancellationToken.None);
        var continued = await _service.ContinueAsync(_id, HostId, false, CancellationToken.None);

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
        await _service.StartAsync(_id, HostId, false, ct);

        // Kings are the cards 11, 24, 37 and 50
        foreach (var king in new[] { 11, 24, 37 })
        {
            await _service.FlipAsync(_id, king, HostId, false, ct);
            await _service.ContinueAsync(_id, HostId, false, ct);
        }

        var fourth = await _service.FlipAsync(_id, 50, HostId, false, ct);
        await _service.ContinueAsync(_id, HostId, false, ct);
        var after = await _service.FlipAsync(_id, 3, HostId, false, ct);

        Assert.False(fourth.IsError);
        Assert.Equal(ErrorOr.ErrorType.Conflict, after.FirstError.Type);
        Assert.Equal("Ringo.GameOver", after.FirstError.Code);
    }

    [Fact]
    public async Task Continuing_IsIdempotentAndNeedsAGameAndAManager()
    {
        var ct = CancellationToken.None;

        Assert.Equal(ErrorOr.ErrorType.NotFound, (await _service.ContinueAsync(_id, HostId, false, ct)).FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, (await _service.ContinueAsync(_id, "guest", false, ct)).FirstError.Type);

        await _service.StartAsync(_id, HostId, false, ct);
        Assert.False((await _service.ContinueAsync(_id, HostId, false, ct)).IsError);
    }

    [Fact]
    public async Task EveryChange_IsPushedToTheSession()
    {
        var ct = CancellationToken.None;

        await _service.StartAsync(_id, HostId, false, ct);
        await _service.FlipAsync(_id, 5, HostId, false, ct);
        await _service.FlipAsync(_id, 5, HostId, false, ct); // repeat: nothing changed
        await _service.FlipAsync(_id, 6, HostId, false, ct); // blocked: nothing changed
        await _service.ContinueAsync(_id, HostId, false, ct);
        await _service.EndAsync(_id, HostId, false, ct);

        Assert.Equal(Enumerable.Repeat(_id, 4), _notifier.RingoChanges);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(52)]
    public async Task FlippingAnUnknownCard_IsRejected(int cardId)
    {
        await _service.StartAsync(_id, HostId, false, CancellationToken.None);

        var result = await _service.FlipAsync(_id, cardId, HostId, false, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public async Task FlippingWithoutAGame_IsNotFound()
    {
        var result = await _service.FlipAsync(_id, 3, HostId, false, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task EndingTheGame_AllowsStartingANewOne()
    {
        await _service.StartAsync(_id, HostId, false, CancellationToken.None);
        await _service.FlipAsync(_id, 3, HostId, false, CancellationToken.None);

        var ended = await _service.EndAsync(_id, HostId, false, CancellationToken.None);
        var restarted = await _service.StartAsync(_id, HostId, false, CancellationToken.None);

        Assert.False(ended.IsError);
        Assert.Empty(restarted.Value.Flips);
    }

    [Fact]
    public async Task EndingWithoutAGame_IsNotFound()
    {
        var result = await _service.EndAsync(_id, HostId, false, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
    }

    private sealed class FakeRingoGamesRepository : IRingoGamesRepository
    {
        private readonly Dictionary<Guid, RingoGame> _games = new();

        public Task<RingoGame?> GetAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(_games.GetValueOrDefault(sessionId));

        public Task<bool> TryCreateAsync(RingoGame game, CancellationToken cancellationToken) =>
            Task.FromResult(_games.TryAdd(game.SessionId, game));

        public Task<RingoFlipResult> FlipAsync(
            Guid sessionId, int cardId, string flippedBy, DateTimeOffset flippedAt, CancellationToken cancellationToken)
        {
            if (!_games.TryGetValue(sessionId, out var game))
            {
                return Task.FromResult(new RingoFlipResult(RingoFlipStatus.NoGame, null));
            }

            if (game.Flips.Any(x => x.CardId == cardId))
            {
                return Task.FromResult(new RingoFlipResult(RingoFlipStatus.AlreadyFlipped, game));
            }

            if (game.IsOver)
            {
                return Task.FromResult(new RingoFlipResult(RingoFlipStatus.GameOver, game));
            }

            if (game.PendingCardId is not null)
            {
                return Task.FromResult(new RingoFlipResult(RingoFlipStatus.CardPending, game));
            }

            game.Flips.Add(new RingoFlip
            {
                SessionId = sessionId,
                Order = game.Flips.Count,
                CardId = cardId,
                FlippedAt = flippedAt,
                FlippedBy = flippedBy,
            });
            game.PendingCardId = cardId;

            return Task.FromResult(new RingoFlipResult(RingoFlipStatus.Flipped, game));
        }

        public Task<RingoGame?> ContinueAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            var game = _games.GetValueOrDefault(sessionId);
            if (game is not null)
            {
                game.PendingCardId = null;
            }

            return Task.FromResult(game);
        }

        public Task<bool> DeleteAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(_games.Remove(sessionId));
    }
}
