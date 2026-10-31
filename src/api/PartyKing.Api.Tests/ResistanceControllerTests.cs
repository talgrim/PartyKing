using ErrorOr;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.API.Controllers.Games.Resistance;
using PartyKing.Contract.V1.Games.Resistance;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Authorization;

namespace PartyKing.Api.Tests;

public class ResistanceControllerTests
{
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _playerId = Guid.NewGuid();
    private readonly FakeResistanceLobbyService _lobbyService = new();
    private readonly FakeResistanceRoundService _roundService = new();
    private readonly FakeResistancePlotCardService _plotCardService = new();
    private readonly ResistanceController _lifecycle;
    private readonly ResistanceRoundController _round;
    private readonly ResistancePlotCardsController _plotCards;

    public ResistanceControllerTests()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddControllers().Services
            .AddSingleton<IAuthenticationService, NoAuthenticationService>()
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var cookies = new FakeCookies(_playerId);
        var context = new ControllerContext { HttpContext = httpContext };

        _lifecycle = new ResistanceController(accessor, _lobbyService, _roundService, cookies) { ControllerContext = context };
        _round = new ResistanceRoundController(accessor, _roundService, _plotCardService, cookies) { ControllerContext = context };
        _plotCards = new ResistancePlotCardsController(accessor, _plotCardService, _lobbyService, cookies) { ControllerContext = context };
    }

    [Fact]
    public async Task Vote_PassesTheChoiceAndTheCookiesPlayerToTheService()
    {
        _roundService.Result = new ErrorOr<ResistanceGameDto>();

        await _round.Vote(_sessionId, new ResistanceVoteRequestDto(false), CancellationToken.None);

        Assert.Equal(("vote", _sessionId, false, _playerId), _roundService.LastCall);
    }

    [Fact]
    public async Task Vote_AnswersWithTheGame()
    {
        var game = Game();
        _roundService.Result = game;

        var result = await _round.Vote(_sessionId, new ResistanceVoteRequestDto(true), CancellationToken.None);

        Assert.Same(game, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Theory]
    [MemberData(nameof(Errors))]
    public async Task Vote_MapsServiceErrorsToTheirStatusCodes(Error error, int status)
    {
        _roundService.Result = error;

        var result = await _round.Vote(_sessionId, new ResistanceVoteRequestDto(true), CancellationToken.None);

        Assert.Equal(status, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task MissionCard_PassesTheChoiceAndTheCookiesPlayerToTheService()
    {
        var game = Game();
        _roundService.Result = game;

        var result = await _round.PlayMissionCard(
            _sessionId, new ResistanceMissionCardRequestDto(false), CancellationToken.None);

        Assert.Equal(("mission", _sessionId, false, _playerId), _roundService.LastCall);
        Assert.Same(game, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Theory]
    [MemberData(nameof(Errors))]
    public async Task MissionCard_MapsServiceErrorsToTheirStatusCodes(Error error, int status)
    {
        _roundService.Result = error;

        var result = await _round.PlayMissionCard(
            _sessionId, new ResistanceMissionCardRequestDto(true), CancellationToken.None);

        Assert.Equal(status, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task Continue_PassesTheCookiesPlayerToTheService()
    {
        _roundService.Result = Game();

        var result = await _round.Continue(_sessionId, CancellationToken.None);

        Assert.Equal(("continue", _sessionId, null, _playerId), _roundService.LastCall);
        Assert.IsType<OkObjectResult>(result);
    }

    [Theory]
    [MemberData(nameof(Errors))]
    public async Task Continue_MapsServiceErrorsToTheirStatusCodes(Error error, int status)
    {
        _roundService.Result = error;

        var result = await _round.Continue(_sessionId, CancellationToken.None);

        Assert.Equal(status, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task Restart_PassesTheCallerToTheService()
    {
        _roundService.Result = Game();

        var result = await _lifecycle.Restart(_sessionId, CancellationToken.None);

        Assert.Equal(("restart", _sessionId, null, _playerId), _roundService.LastCall);
        Assert.IsType<OkObjectResult>(result);
    }

    [Theory]
    [MemberData(nameof(Errors))]
    public async Task Restart_MapsServiceErrorsToTheirStatusCodes(Error error, int status)
    {
        _roundService.Result = error;

        var result = await _lifecycle.Restart(_sessionId, CancellationToken.None);

        Assert.Equal(status, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task PlayPlotCard_PassesTheCardTheTargetAndTheCookiesPlayerToTheService()
    {
        var target = Guid.NewGuid();
        var game = Game();
        _plotCardService.Result = game;

        var result = await _plotCards.Play(
            _sessionId, 4, new ResistancePlayPlotCardRequestDto(target), CancellationToken.None);

        Assert.Equal(($"play-4-{target}", _sessionId, null, _playerId), _plotCardService.LastCall);
        Assert.Same(game, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Theory]
    [MemberData(nameof(Errors))]
    public async Task PlayPlotCard_MapsServiceErrorsToTheirStatusCodes(Error error, int status)
    {
        _plotCardService.Result = error;

        var result = await _plotCards.Play(
            _sessionId, 4, new ResistancePlayPlotCardRequestDto(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(status, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task ConfirmPlotCard_PassesTheCardAndTheCookiesPlayerToTheService()
    {
        _plotCardService.Result = Game();

        var result = await _plotCards.Confirm(_sessionId, 7, CancellationToken.None);

        Assert.Equal(("confirm-7", _sessionId, null, _playerId), _plotCardService.LastCall);
        Assert.IsType<OkObjectResult>(result);
    }

    [Theory]
    [MemberData(nameof(Errors))]
    public async Task ConfirmPlotCard_MapsServiceErrorsToTheirStatusCodes(Error error, int status)
    {
        _plotCardService.Result = error;

        var result = await _plotCards.Confirm(_sessionId, 7, CancellationToken.None);

        Assert.Equal(status, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task DealPlotCards_PassesTheCookiesPlayerToTheService()
    {
        _plotCardService.Result = Game();

        var result = await _plotCards.Deal(
            _sessionId, new ResistanceDealPlotCardsRequestDto([]), CancellationToken.None);

        Assert.Equal(("deal-plot-cards", _sessionId, null, _playerId), _plotCardService.LastCall);
        Assert.IsType<OkObjectResult>(result);
    }

    public static TheoryData<Error, int> Errors => new()
    {
        { Error.Forbidden("Resistance.NotPlayer", "x"), StatusCodes.Status403Forbidden },
        { Error.Conflict("Resistance.NotVoting", "x"), StatusCodes.Status409Conflict },
        { Error.Conflict("Resistance.GameOver", "x"), StatusCodes.Status409Conflict },
        { Error.NotFound("Resistance.NotFound", "x"), StatusCodes.Status404NotFound },
    };

    private static ResistanceGameDto Game() => new(
        DateTimeOffset.UtcNow, true, [], null, 10, null, null, null, null, 1, null, [], null, 0, null, [], null,
        null, null, null, new ResistanceRulesDto([], false, 1), null, [], null, [], [], [], [], [], [], [], null, null, [], []);

    /// <summary>Only the player cookie of the Resistance game is read; the rest is not used by the controller.</summary>
    private sealed class FakeCookies : ICookieAuthorizationHandler
    {
        private readonly Guid _playerId;

        public FakeCookies(Guid playerId) => _playerId = playerId;

        public bool TryReadGamePlayer(GameKind game, HttpRequest request, Guid sessionId, out Guid playerId)
        {
            playerId = _playerId;
            return game == GameKind.Resistance;
        }

        public bool TryReadProfileId(HttpRequest request, out string profileId)
        {
            profileId = string.Empty;
            return false;
        }

        public void Authorize(string profileId, HttpContext? httpContext) => throw new NotSupportedException();
        public void Authorize(string spotifyAccessToken, int expiresIn, HttpContext? httpContext) => throw new NotSupportedException();
        public void AuthorizeAdmin(Guid adminId, HttpContext? httpContext) => throw new NotSupportedException();
        public bool TryReadAdminId(HttpRequest request, out Guid adminId) => throw new NotSupportedException();
        public void SignOutAdmin(HttpContext? httpContext) => throw new NotSupportedException();
        public void GrantSessionAccess(Guid sessionId, string stamp, HttpContext? httpContext) => throw new NotSupportedException();
        public bool TryReadSessionAccess(HttpRequest request, Guid sessionId, out string stamp) => throw new NotSupportedException();
        public void GrantGamePlayer(GameKind game, Guid sessionId, Guid playerId, HttpContext? httpContext) => throw new NotSupportedException();
    }

    private sealed class NoAuthenticationService : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, System.Security.Claims.ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }
}
