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
        _service = new RingoService(sessionService, _games, clock);
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

        Assert.Empty(started.Value.FlippedCards);
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
        await _service.FlipAsync(_id, 4, HostId, false, CancellationToken.None);
        var result = await _service.FlipAsync(_id, 30, HostId, false, CancellationToken.None);

        Assert.Equal([30, 4], result.Value.FlippedCards);
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
        Assert.Empty(restarted.Value.FlippedCards);
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

        public Task<RingoGame?> FlipAsync(Guid sessionId, int cardId, CancellationToken cancellationToken)
        {
            if (!_games.TryGetValue(sessionId, out var game))
            {
                return Task.FromResult<RingoGame?>(null);
            }

            var flipped = game.FlippedCards.Length == 0
                ? []
                : game.FlippedCards.Split(',').Select(int.Parse).ToList();
            if (!flipped.Contains(cardId))
            {
                flipped.Add(cardId);
                game.FlippedCards = string.Join(',', flipped);
            }

            return Task.FromResult<RingoGame?>(game);
        }

        public Task<bool> DeleteAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(_games.Remove(sessionId));
    }
}
