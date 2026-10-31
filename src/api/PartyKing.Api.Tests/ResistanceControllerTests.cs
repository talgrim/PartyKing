using ErrorOr;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.API.Controllers.Games.Resistance;
using PartyKing.Application.Games.Services;
using PartyKing.Contract.V1.Games.Resistance;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Authorization;

namespace PartyKing.Api.Tests;

public class ResistanceControllerTests
{
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _playerId = Guid.NewGuid();
    private readonly FakeResistanceService _service = new();
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

        _lifecycle = new ResistanceController(accessor, _service, cookies) { ControllerContext = context };
        _round = new ResistanceRoundController(accessor, _service, cookies) { ControllerContext = context };
        _plotCards = new ResistancePlotCardsController(accessor, _service, cookies) { ControllerContext = context };
    }

    [Fact]
    public async Task Vote_PassesTheChoiceAndTheCookiesPlayerToTheService()
    {
        _service.Result = new ErrorOr<ResistanceGameDto>();

        await _round.Vote(_sessionId, new ResistanceVoteRequestDto(false), CancellationToken.None);

        Assert.Equal(("vote", _sessionId, false, _playerId), _service.LastCall);
    }

    [Fact]
    public async Task Vote_AnswersWithTheGame()
    {
        var game = Game();
        _service.Result = game;

        var result = await _round.Vote(_sessionId, new ResistanceVoteRequestDto(true), CancellationToken.None);

        Assert.Same(game, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Theory]
    [MemberData(nameof(Errors))]
    public async Task Vote_MapsServiceErrorsToTheirStatusCodes(Error error, int status)
    {
        _service.Result = error;

        var result = await _round.Vote(_sessionId, new ResistanceVoteRequestDto(true), CancellationToken.None);

        Assert.Equal(status, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task MissionCard_PassesTheChoiceAndTheCookiesPlayerToTheService()
    {
        var game = Game();
        _service.Result = game;

        var result = await _round.PlayMissionCard(
            _sessionId, new ResistanceMissionCardRequestDto(false), CancellationToken.None);

        Assert.Equal(("mission", _sessionId, false, _playerId), _service.LastCall);
        Assert.Same(game, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Theory]
    [MemberData(nameof(Errors))]
    public async Task MissionCard_MapsServiceErrorsToTheirStatusCodes(Error error, int status)
    {
        _service.Result = error;

        var result = await _round.PlayMissionCard(
            _sessionId, new ResistanceMissionCardRequestDto(true), CancellationToken.None);

        Assert.Equal(status, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task Continue_PassesTheCookiesPlayerToTheService()
    {
        _service.Result = Game();

        var result = await _round.Continue(_sessionId, CancellationToken.None);

        Assert.Equal(("continue", _sessionId, null, _playerId), _service.LastCall);
        Assert.IsType<OkObjectResult>(result);
    }

    [Theory]
    [MemberData(nameof(Errors))]
    public async Task Continue_MapsServiceErrorsToTheirStatusCodes(Error error, int status)
    {
        _service.Result = error;

        var result = await _round.Continue(_sessionId, CancellationToken.None);

        Assert.Equal(status, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task Restart_PassesTheCallerToTheService()
    {
        _service.Result = Game();

        var result = await _lifecycle.Restart(_sessionId, CancellationToken.None);

        Assert.Equal(("restart", _sessionId, null, _playerId), _service.LastCall);
        Assert.IsType<OkObjectResult>(result);
    }

    [Theory]
    [MemberData(nameof(Errors))]
    public async Task Restart_MapsServiceErrorsToTheirStatusCodes(Error error, int status)
    {
        _service.Result = error;

        var result = await _lifecycle.Restart(_sessionId, CancellationToken.None);

        Assert.Equal(status, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task PlayPlotCard_PassesTheCardTheTargetAndTheCookiesPlayerToTheService()
    {
        var target = Guid.NewGuid();
        var game = Game();
        _service.Result = game;

        var result = await _plotCards.Play(
            _sessionId, 4, new ResistancePlayPlotCardRequestDto(target), CancellationToken.None);

        Assert.Equal(($"play-4-{target}", _sessionId, null, _playerId), _service.LastCall);
        Assert.Same(game, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Theory]
    [MemberData(nameof(Errors))]
    public async Task PlayPlotCard_MapsServiceErrorsToTheirStatusCodes(Error error, int status)
    {
        _service.Result = error;

        var result = await _plotCards.Play(
            _sessionId, 4, new ResistancePlayPlotCardRequestDto(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(status, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task ConfirmPlotCard_PassesTheCardAndTheCookiesPlayerToTheService()
    {
        _service.Result = Game();

        var result = await _plotCards.Confirm(_sessionId, 7, CancellationToken.None);

        Assert.Equal(("confirm-7", _sessionId, null, _playerId), _service.LastCall);
        Assert.IsType<OkObjectResult>(result);
    }

    [Theory]
    [MemberData(nameof(Errors))]
    public async Task ConfirmPlotCard_MapsServiceErrorsToTheirStatusCodes(Error error, int status)
    {
        _service.Result = error;

        var result = await _plotCards.Confirm(_sessionId, 7, CancellationToken.None);

        Assert.Equal(status, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task DealPlotCards_PassesTheCookiesPlayerToTheService()
    {
        _service.Result = Game();

        var result = await _plotCards.Deal(
            _sessionId, new ResistanceDealPlotCardsRequestDto([]), CancellationToken.None);

        Assert.Equal(("deal-plot-cards", _sessionId, null, _playerId), _service.LastCall);
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

    private sealed class FakeResistanceService : IResistanceService
    {
        public ErrorOr<ResistanceGameDto> Result { get; set; }

        public (string Action, Guid SessionId, bool? Approve, Guid? PlayerId)? LastCall { get; private set; }

        public Task<ErrorOr<ResistanceGameDto>> VoteAsync(
            Guid sessionId, bool approve, GameCaller caller, CancellationToken cancellationToken)
        {
            LastCall = ("vote", sessionId, approve, caller.PlayerId);
            return Task.FromResult(Result);
        }

        public Task<ErrorOr<ResistanceGameDto>> PlayMissionCardAsync(
            Guid sessionId, bool success, GameCaller caller, CancellationToken cancellationToken)
        {
            LastCall = ("mission", sessionId, success, caller.PlayerId);
            return Task.FromResult(Result);
        }

        public Task<ErrorOr<ResistanceGameDto>> ContinueAsync(
            Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
        {
            LastCall = ("continue", sessionId, null, caller.PlayerId);
            return Task.FromResult(Result);
        }

        public Task<ErrorOr<ResistanceGameDto>> TakeResponsibilityAsync(Guid sessionId, int cardId, Guid targetPlayerId, bool? takeOpinionMaker, GameCaller caller, CancellationToken cancellationToken)
        {
            LastCall = ($"take-{cardId}-{targetPlayerId}-{takeOpinionMaker}", sessionId, null, caller.PlayerId);
            return Task.FromResult(Result);
        }

        public Task<ErrorOr<ResistanceGameDto>> TakeLeadAsync(Guid sessionId, int cardId, GameCaller caller, CancellationToken cancellationToken)
        {
            LastCall = ($"take-lead-{cardId}", sessionId, null, caller.PlayerId);
            return Task.FromResult(Result);
        }

        public Task<ErrorOr<ResistanceGameDto>> PeekMissionCardAsync(Guid sessionId, int cardId, Guid targetPlayerId, GameCaller caller, CancellationToken cancellationToken)
        {
            LastCall = ($"peek-{cardId}", sessionId, null, caller.PlayerId);
            return Task.FromResult(Result);
        }

        public Task<ErrorOr<ResistanceGameDto>> PlayNoConfidenceAsync(Guid sessionId, int cardId, GameCaller caller, CancellationToken cancellationToken)
        {
            LastCall = ($"no-confidence-{cardId}", sessionId, null, caller.PlayerId);
            return Task.FromResult(Result);
        }

        public Task<ErrorOr<ResistanceGameDto>> PassLeaderWindowAsync(Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
        {
            LastCall = ("pass-leader-window", sessionId, null, caller.PlayerId);
            return Task.FromResult(Result);
        }

        public Task<ErrorOr<ResistanceGameDto>> PlayPlotCardAsync(Guid sessionId, int cardId, Guid targetPlayerId, GameCaller caller, CancellationToken cancellationToken)
        {
            LastCall = ($"play-{cardId}-{targetPlayerId}", sessionId, null, caller.PlayerId);
            return Task.FromResult(Result);
        }

        public Task<ErrorOr<ResistanceGameDto>> ConfirmPlotCardAsync(Guid sessionId, int cardId, GameCaller caller, CancellationToken cancellationToken)
        {
            LastCall = ($"confirm-{cardId}", sessionId, null, caller.PlayerId);
            return Task.FromResult(Result);
        }

        public Task<ErrorOr<ResistanceGameDto>> DealPlotCardsAsync(Guid sessionId, IReadOnlyList<ResistancePlotCardAssignmentDto>? assignments, GameCaller caller, CancellationToken cancellationToken)
        {
            LastCall = ("deal-plot-cards", sessionId, null, caller.PlayerId);
            return Task.FromResult(Result);
        }

        public Task<ErrorOr<ResistanceGameDto>> SetPlotCardsAsync(Guid sessionId, bool enabled, GameCaller caller, CancellationToken cancellationToken)
        {
            LastCall = ("plot-cards", sessionId, null, caller.PlayerId);
            return Task.FromResult(Result);
        }

        public Task<ErrorOr<ResistanceGameDto>> RefreshRulesAsync(Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
        {
            LastCall = ("refresh-rules", sessionId, null, caller.PlayerId);
            return Task.FromResult(Result);
        }

        public Task<ErrorOr<ResistanceGameDto>> GetAsync(Guid sessionId, GameCaller caller, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ErrorOr<ResistanceLobbyDto>> GetLobbyAsync(Guid sessionId, GameCaller caller, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ErrorOr<ResistanceGameDto>> CreateAsync(Guid sessionId, GameCaller caller, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ErrorOr<ResistanceGameDto>> OpenAsync(Guid sessionId, GameCaller caller, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ErrorOr<Guid>> JoinAsync(Guid sessionId, string? name, GameCaller caller, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ErrorOr<ResistanceGameDto>> StartAsync(Guid sessionId, GameCaller caller, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ErrorOr<ResistanceGameDto>> RestartAsync(
            Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
        {
            LastCall = ("restart", sessionId, null, caller.PlayerId);
            return Task.FromResult(Result);
        }

        public Task<ErrorOr<ResistanceGameDto>> SelectTeamAsync(Guid sessionId, IReadOnlyList<Guid>? playerIds, GameCaller caller, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ErrorOr<ResistanceGameDto>> KickAsync(Guid sessionId, Guid playerId, GameCaller caller, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ErrorOr<ResistanceGameDto>> ReorderAsync(Guid sessionId, IReadOnlyList<Guid>? playerIds, GameCaller caller, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ErrorOr<Deleted>> EndAsync(Guid sessionId, GameCaller caller, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

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
