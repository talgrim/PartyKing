using ErrorOr;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.API.Controllers.Games.Resistance;
using PartyKing.API.Cookies;
using PartyKing.Application.Games.Services.Base;
using PartyKing.Contract.V1.Games.Resistance;
using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.Api.Tests;

public class ResistanceSeatsControllerTests
{
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _playerId = Guid.NewGuid();
    private readonly FakeResistanceSeatService _seats = new();
    private readonly SignedCookies _cookies = new(DataProtectionProvider.Create("partyking-tests"));
    private readonly DefaultHttpContext _httpContext;
    private readonly ResistanceSeatsController _controller;

    public ResistanceSeatsControllerTests()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddControllers().Services
            .AddSingleton<IAuthenticationService, NoAuthentication>()
            .BuildServiceProvider();
        _httpContext = new DefaultHttpContext { RequestServices = services };
        var accessor = new HttpContextAccessor { HttpContext = _httpContext };
        _controller = new ResistanceSeatsController(accessor, _seats, _cookies)
        {
            ControllerContext = new ControllerContext { HttpContext = _httpContext },
        };
    }

    [Fact]
    public async Task Claim_RemembersTheSeatWithItsNewStampInTheCookie()
    {
        var stamp = Guid.NewGuid();
        _seats.ClaimResult = new GameSeat(_playerId, stamp);

        var result = await _controller.Claim(_sessionId, new ResistanceClaimSeatRequestDto(_playerId), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal([(_sessionId, _playerId)], _seats.Claims);
        var setCookie = _httpContext.Response.Headers.SetCookie.ToString();
        var request = new DefaultHttpContext().Request;
        request.Headers.Cookie = setCookie.Split(';')[0];
        Assert.True(_cookies.TryReadGamePlayer(GameKind.Resistance, request, _sessionId, out var seat));
        Assert.Equal(new GameSeat(_playerId, stamp), seat);
    }

    [Fact]
    public async Task Claim_ThatFails_SetsNoCookie()
    {
        _seats.ClaimResult = Error.Conflict("Resistance.SeatNotReleased", "x");

        var result = await _controller.Claim(_sessionId, new ResistanceClaimSeatRequestDto(_playerId), CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Equal(string.Empty, _httpContext.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public async Task SetReleased_PassesTheChoiceAndTheCaller()
    {
        var result = await _controller.SetReleased(
            _sessionId, _playerId, new ResistanceSeatReleaseRequestDto(true), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var (player, released, _) = _seats.LastRelease!.Value;
        Assert.Equal(_playerId, player);
        Assert.True(released);
    }

    [Fact]
    public async Task GetClaimable_ListsTheSeats()
    {
        var result = await _controller.GetClaimable(_sessionId, CancellationToken.None);

        var seats = Assert.IsAssignableFrom<IReadOnlyList<ResistanceClaimableSeatDto>>(
            Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("Ola", Assert.Single(seats).Name);
    }

    private sealed class NoAuthentication : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, System.Security.Claims.ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }
}
