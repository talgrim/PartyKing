using PartyKing.Application.Admin.Services;
using PartyKing.Application.Games.Services;
using PartyKing.Application.Localization;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.Sessions;

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
        var sessionAuthorizer = new SessionAuthorizer(_sessions);
        _service = new GameService(sessionAuthorizer);
        _id = _sessions.AddSession(HostId, new PasswordHasher().Hash("secret")).Id;
    }

    [Fact]
    public async Task Guests_AreForbidden()
    {
        var result = await _service.ListAsync(_id, new SessionCaller("someone-else", false), UiLanguage.En, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.Forbidden, result.FirstError.Type);
    }

    [Fact]
    public async Task AnUnknownSession_IsNotFound()
    {
        var result = await _service.ListAsync(Guid.NewGuid(), new SessionCaller(HostId, false), UiLanguage.En, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
    }

    [Theory]
    [InlineData(HostId, false)]
    [InlineData(null, true)]
    public async Task TheHostAndAdmins_GetThePlaceholderGames(string? profileId, bool isAdmin)
    {
        var result = await _service.ListAsync(_id, new SessionCaller(profileId, isAdmin), UiLanguage.En, CancellationToken.None);

        Assert.Equal(["resistance", "ringo", "never-have-i-ever"], result.Value.Select(game => game.Id));
    }

    [Theory]
    [InlineData(UiLanguage.En, "Never have I ever")]
    [InlineData(UiLanguage.Pl, "Nigdy w życiu")]
    public async Task TheNeverHaveIEverGame_IsNamedInTheLanguage(UiLanguage language, string name)
    {
        var result = await _service.ListAsync(_id, new SessionCaller(HostId, false), language, CancellationToken.None);

        Assert.Equal(name, result.Value.Single(game => game.Id == "never-have-i-ever").Name);
    }

    [Theory]
    [InlineData(UiLanguage.En)]
    [InlineData(UiLanguage.Pl)]
    public async Task RingoAndResistance_KeepTheirNamesInEveryLanguage(UiLanguage language)
    {
        var result = await _service.ListAsync(_id, new SessionCaller(HostId, false), language, CancellationToken.None);

        Assert.Equal("Ringo", result.Value.Single(game => game.Id == "ringo").Name);
        Assert.Equal("Resistance", result.Value.Single(game => game.Id == "resistance").Name);
    }

    [Fact]
    public async Task TheDescriptions_DifferBetweenLanguages()
    {
        var english = await _service.ListAsync(_id, new SessionCaller(HostId, false), UiLanguage.En, CancellationToken.None);
        var polish = await _service.ListAsync(_id, new SessionCaller(HostId, false), UiLanguage.Pl, CancellationToken.None);

        Assert.All(
            english.Value.Zip(polish.Value),
            pair => Assert.NotEqual(pair.First.Description, pair.Second.Description));
    }
}
