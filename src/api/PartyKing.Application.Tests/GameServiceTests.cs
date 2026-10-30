using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Admin.Services;
using PartyKing.Application.Games.Services;
using PartyKing.Application.Player.Services;
using PartyKing.Application.Realtime;
using PartyKing.Application.Sessions.Services;

namespace PartyKing.Application.Tests;

public class GameServiceTests
{
    private const string HostId = "host-1";

    private readonly FakeSessionsRepository _sessions = new();
    private readonly GameService _service;
    private readonly Guid _id;

    public GameServiceTests()
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
        _service = new GameService(sessionService);
        _id = _sessions.AddSession(HostId, new PasswordHasher().Hash("secret")).Id;
    }

    [Fact]
    public async Task Guests_AreForbidden()
    {
        var result = await _service.ListAsync(_id, "someone-else", false, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.Forbidden, result.FirstError.Type);
    }

    [Fact]
    public async Task AnUnknownSession_IsNotFound()
    {
        var result = await _service.ListAsync(Guid.NewGuid(), HostId, false, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
    }

    [Theory]
    [InlineData(HostId, false)]
    [InlineData(null, true)]
    public async Task TheHostAndAdmins_GetThePlaceholderGames(string? profileId, bool isAdmin)
    {
        var result = await _service.ListAsync(_id, profileId, isAdmin, CancellationToken.None);

        Assert.Equal(["resistance", "ringo", "never-have-i-ever"], result.Value.Select(game => game.Id));
    }
}
