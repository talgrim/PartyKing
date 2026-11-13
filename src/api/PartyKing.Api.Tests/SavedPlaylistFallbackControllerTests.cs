using PartyKing.Application.Demo.Models;
using System.Reflection;
using System.Security.Claims;
using ErrorOr;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.API.Authorization;
using PartyKing.API.Controllers;
using PartyKing.API.Cookies;
using PartyKing.Application.Accounts;
using PartyKing.Application.Games.Services.Base;
using PartyKing.Application.Playlists.Services;
using PartyKing.Application.Queue.Services;
using PartyKing.Application.Sessions;
using PlaylistSection = PartyKing.Contract.V1.Enums.PlaylistSection;
using PartyKing.Contract.V1.Playlists;
using PartyKing.Contract.V1.Sessions;
using PartyKing.Contract.V1.Spotify;
using PartyKing.Domain.Entities.Auth;
using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.Api.Tests;

/// <summary>
/// Choosing a saved playlist as the fallback: the account says whose playlists they are, the host cookie or the
/// admin account says who may change the session.
/// </summary>
public class SavedPlaylistFallbackControllerTests
{
    private const string HostProfileId = "host-profile";

    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly FakeFallbackPlaylistService _fallbackService = new();
    private readonly FakeSavedPlaylistService _savedService = new();
    private readonly FallbackPlaylistController _fallback;
    private readonly AccountPlaylistsController _accountPlaylists;

    public SavedPlaylistFallbackControllerTests()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddControllers().Services
            .AddSingleton<IAuthenticationService, NoAuthenticationService>()
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, _accountId.ToString())], "account"))
        };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var context = new ControllerContext { HttpContext = httpContext };

        _fallback = new FallbackPlaylistController(accessor, _fallbackService, new FakeCookies(HostProfileId))
        {
            ControllerContext = context
        };
        _accountPlaylists = new AccountPlaylistsController(accessor, _savedService) { ControllerContext = context };
    }

    [Fact]
    public async Task SetFallbackSavedPlaylist_PassesTheChoiceTheAccountAndTheHostToTheService()
    {
        var playlistId = Guid.NewGuid();

        var result = await _fallback.SetFallbackSavedPlaylist(
            _sessionId,
            new SetFallbackSavedPlaylistRequestDto(playlistId, Shuffle: true),
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(
            (_sessionId, playlistId, true, _accountId, new SessionCaller(HostProfileId, false)),
            _fallbackService.LastSavedCall);
    }

    [Fact]
    public async Task SetFallbackSavedPlaylist_AnswersWithTheServicesError()
    {
        _fallbackService.SavedResult = Error.NotFound(description: "The playlist was not found");

        var result = await _fallback.SetFallbackSavedPlaylist(
            _sessionId,
            new SetFallbackSavedPlaylistRequestDto(Guid.NewGuid(), Shuffle: false),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task SetFallbackSavedPlaylist_AnswersForbiddenToSomeoneWhoDoesNotManageTheSession()
    {
        _fallbackService.SavedResult = Error.Forbidden(description: "Only the host or an admin can change the session settings");

        var result = await _fallback.SetFallbackSavedPlaylist(
            _sessionId,
            new SetFallbackSavedPlaylistRequestDto(Guid.NewGuid(), Shuffle: false),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public void SetFallbackSavedPlaylist_NeedsAnAccount_AndAccessToTheSession_ButNotTheHostCookie()
    {
        var method = typeof(FallbackPlaylistController).GetMethod(nameof(FallbackPlaylistController.SetFallbackSavedPlaylist))!;

        Assert.NotNull(method.GetCustomAttribute<AuthorizeAccountAttribute>());
        Assert.NotNull(method.GetCustomAttribute<RequireSessionAccessAttribute>());

        // The host cookie scheme would turn the account away, and a host without an account is turned away above
        Assert.Null(method.GetCustomAttribute<AuthorizeByCookieAttribute>());
    }

    [Fact]
    public void SetFallbackSavedPlaylist_IsAPutUnderTheSession()
    {
        var method = typeof(FallbackPlaylistController).GetMethod(nameof(FallbackPlaylistController.SetFallbackSavedPlaylist))!;

        var attribute = Assert.IsType<HttpPutAttribute>(method.GetCustomAttributes<HttpMethodAttribute>().Single());

        Assert.Equal("sessions/{sessionId:guid}/fallback-saved-playlist", attribute.Template);
    }

    [Fact]
    public async Task GetSavedPlaylists_ListsThePlaylistsOfTheSignedInAccount()
    {
        var playlists = new List<SavedPlaylistDto> { new(Guid.NewGuid(), "Friday", DateTime.UtcNow, 3) };
        _savedService.ListResult = playlists;

        var result = await _accountPlaylists.GetSavedPlaylists(CancellationToken.None);

        Assert.Same(playlists, Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(_accountId, _savedService.ListedAccountId);
    }

    [Fact]
    public void GetSavedPlaylists_NeedsAnAccount_AndNoSession()
    {
        var method = typeof(AccountPlaylistsController).GetMethod(nameof(AccountPlaylistsController.GetSavedPlaylists))!;

        Assert.NotNull(method.GetCustomAttribute<AuthorizeAccountAttribute>());
        Assert.Null(method.GetCustomAttribute<RequireSessionAccessAttribute>());
        Assert.Equal("account/saved-playlists", method.GetCustomAttributes<HttpGetAttribute>().Single().Template);
    }

    [Fact]
    public void TheAccountPlaylistsController_OnlyLists()
    {
        var actions = typeof(AccountPlaylistsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(x => x.Name);

        Assert.Equal(["GetSavedPlaylists"], actions);
    }

    private sealed class FakeFallbackPlaylistService : IFallbackPlaylistService
    {
        public ErrorOr<Success> SavedResult { get; set; } = Result.Success;

        public (Guid SessionId, Guid PlaylistId, bool Shuffle, Guid AccountId, SessionCaller Caller)? LastSavedCall { get; private set; }

        public Task<ErrorOr<Success>> SetSavedAsync(
            Guid sessionId,
            Guid savedPlaylistId,
            bool shuffle,
            Guid accountId,
            SessionCaller caller,
            CancellationToken cancellationToken)
        {
            LastSavedCall = (sessionId, savedPlaylistId, shuffle, accountId, caller);

            return Task.FromResult(SavedResult);
        }

        public Task<ErrorOr<Success>> SetAsync(
            Guid sessionId,
            string? playlist,
            bool shuffle,
            SessionCaller caller,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ErrorOr<IReadOnlyList<PlaylistDto>>> GetHostPlaylistsAsync(
            Guid sessionId,
            string profileId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeSavedPlaylistService : ISavedPlaylistService
    {
        public IReadOnlyList<SavedPlaylistDto> ListResult { get; set; } = [];

        public Guid? ListedAccountId { get; private set; }

        public Task<ErrorOr<IReadOnlyList<SavedPlaylistDto>>> ListAsync(
            Guid accountId,
            CancellationToken cancellationToken)
        {
            ListedAccountId = accountId;

            return Task.FromResult(ErrorOrFactory.From(ListResult));
        }

        public Task<ErrorOr<SavedPlaylistDto>> SaveQueueAsync(
            Guid accountId,
            Guid sessionId,
            string? name,
            IReadOnlyCollection<PlaylistSection> sections,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ErrorOr<SavedPlaylistPreviewDto>> GetPreviewAsync(
            Guid sessionId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeCookies(string hostProfileId) : ISignedCookies
    {
        public bool TryReadProfileId(HttpRequest request, out string profileId)
        {
            profileId = hostProfileId;

            return true;
        }

        public bool TryReadAccount(HttpRequest request, out Guid accountId, out Guid securityStamp, out AccountRole role)
        {
            accountId = Guid.Empty;
            securityStamp = Guid.Empty;
            role = AccountRole.User;

            return false;
        }

        public void GrantHost(string profileId, HttpContext? httpContext) => throw new NotSupportedException();
        public void GrantSpotifyAccessToken(string spotifyAccessToken, int expiresIn, HttpContext? httpContext) => throw new NotSupportedException();
        public void GrantAccount(AccountIdentity identity, HttpContext? httpContext) => throw new NotSupportedException();
        public void RevokeAccount(HttpContext? httpContext) => throw new NotSupportedException();
        public void GrantSessionAccess(Guid sessionId, string stamp, HttpContext? httpContext) => throw new NotSupportedException();
        public bool TryReadSessionAccess(HttpRequest request, Guid sessionId, out string stamp) => throw new NotSupportedException();
        public void GrantGamePlayer(GameKind game, Guid sessionId, GameSeat seat, HttpContext? httpContext) => throw new NotSupportedException();
        public void GrantDemoSession(Guid sessionId, string profileId, HttpContext? httpContext) => throw new NotSupportedException();
        public IReadOnlyList<StartedDemoSession> ReadDemoSessions(HttpRequest request) => throw new NotSupportedException();
        public bool TryReadGamePlayer(GameKind game, HttpRequest request, Guid sessionId, out GameSeat seat) => throw new NotSupportedException();
    }

    private sealed class NoAuthenticationService : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }
}
