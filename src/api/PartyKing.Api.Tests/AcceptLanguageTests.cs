using PartyKing.Application.Ports;
using ErrorOr;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.API.Controllers.Games;
using PartyKing.Application.Games.Services;
using PartyKing.Application.Localization;
using PartyKing.Application.Sessions;
using PartyKing.Contract.V1.Games;
using PartyKing.Domain.Entities.Games;

namespace PartyKing.Api.Tests;

/// <summary>The controllers hand the <c>Accept-Language</c> language on to the services.</summary>
public class AcceptLanguageTests
{
    private readonly FakeGameService _service = new();
    private readonly DefaultHttpContext _httpContext;
    private readonly GamesController _controller;

    public AcceptLanguageTests()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddControllers().Services
            .AddSingleton<IAuthenticationService, NoAuthenticationService>()
            .BuildServiceProvider();
        _httpContext = new DefaultHttpContext { RequestServices = services };

        _controller = new GamesController(
            new HttpContextAccessor { HttpContext = _httpContext },
            _service,
            new FakeCookies())
        {
            ControllerContext = new ControllerContext { HttpContext = _httpContext },
        };
    }

    [Theory]
    [InlineData("pl", UiLanguage.Pl)]
    [InlineData("pl-PL,pl;q=0.9,en;q=0.8", UiLanguage.Pl)]
    [InlineData("en-US", UiLanguage.En)]
    [InlineData("de", UiLanguage.En)]
    public async Task TheGameList_IsRequestedInTheHeadersLanguage(string header, UiLanguage expected)
    {
        _httpContext.Request.Headers.AcceptLanguage = header;

        await _controller.GetAll(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(expected, _service.LastLanguage);
    }

    [Fact]
    public async Task WithoutTheHeader_TheGameListIsRequestedInEnglish()
    {
        await _controller.GetAll(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(UiLanguage.En, _service.LastLanguage);
    }

    private sealed class FakeGameService : IGameService
    {
        public UiLanguage? LastLanguage { get; private set; }

        public Task<ErrorOr<IReadOnlyList<GameDto>>> ListAsync(
            Guid sessionId, SessionCaller caller, UiLanguage language, CancellationToken cancellationToken)
        {
            LastLanguage = language;
            return Task.FromResult(ErrorOrFactory.From<IReadOnlyList<GameDto>>([]));
        }
    }

    private sealed class FakeCookies : ICookieAuthorizationHandler
    {
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
        public bool TryReadGamePlayer(GameKind game, HttpRequest request, Guid sessionId, out Guid playerId) => throw new NotSupportedException();
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
