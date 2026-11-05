using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Games.Services.Base;
using PartyKing.Application.Games.Services.Resistance;
using PartyKing.Application.Realtime;
using PartyKing.Application.Sessions.Services;
using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.Application.Tests;

public class ResistancePresenceServiceTests : ResistanceServiceTestBase
{
    private readonly GamePresence _presence;
    private readonly ResistancePresenceService _service;

    public ResistancePresenceServiceTests()
    {
        _presence = new GamePresence(_notifier, NullLogger<GamePresence>.Instance, TimeSpan.FromSeconds(30));
        var lobby = new ResistanceGameLobby(new SessionAuthorizer(_sessions), _sessions, _games, new FakeClock());
        _service = new ResistancePresenceService(lobby, _presence);
    }

    [Fact]
    public async Task GetAsync_ListsTheOnlinePlayersForTheHost()
    {
        var player = Guid.NewGuid();
        await _presence.ConnectAsync(GameKind.Resistance, _id, player, "c1");

        var result = await _service.GetAsync(_id, Host, CancellationToken.None);

        Assert.Equal([player], result.Value.OnlinePlayerIds);
    }

    [Fact]
    public async Task GetAsync_AnAdminMayAsk()
    {
        var result = await _service.GetAsync(_id, Admin, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Empty(result.Value.OnlinePlayerIds);
    }

    [Fact]
    public async Task GetAsync_APlayerIsForbidden()
    {
        var result = await _service.GetAsync(_id, new GameCaller(null, false, Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.Forbidden, result.FirstError.Type);
    }

    [Fact]
    public async Task GetAsync_AnUnknownSessionIsNotFound()
    {
        var result = await _service.GetAsync(Guid.NewGuid(), Admin, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
    }
}
