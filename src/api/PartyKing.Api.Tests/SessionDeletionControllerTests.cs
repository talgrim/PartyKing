using System.Reflection;
using ErrorOr;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.API.Authorization;
using PartyKing.API.Controllers;
using PartyKing.API.Cookies;
using PartyKing.Application.Sessions;
using PartyKing.Application.Sessions.Services;
using PartyKing.Contract.V1.Sessions;

namespace PartyKing.Api.Tests;

/// <summary>
/// The three endpoints of session deletion: the host or an admin deletes at once, any guest with access asks for it
/// to happen after the delay, and only an admin calls the request off. The rules themselves are the services'.
/// </summary>
public class SessionDeletionControllerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly FakeSessionService _sessions = new();
    private readonly FakeSessionAdminService _admin = new();
    private readonly SignedCookies _cookies = new(DataProtectionProvider.Create("partyking-tests"));
    private readonly DefaultHttpContext _httpContext;
    private readonly SessionsController _controller;
    private readonly AdminController _adminController;

    public SessionDeletionControllerTests()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddControllers().Services
            .AddSingleton<IAuthenticationService, NoAuthenticationService>()
            .BuildServiceProvider();
        _httpContext = new DefaultHttpContext { RequestServices = services };
        var accessor = new HttpContextAccessor { HttpContext = _httpContext };
        var context = new ControllerContext { HttpContext = _httpContext };

        _controller = new SessionsController(accessor, _sessions, null!, null!, null!, _cookies)
        {
            ControllerContext = context
        };
        _adminController = new AdminController(accessor, _admin)
        {
            ControllerContext = context
        };
    }

    /// <summary>The browser sends back the host cookie the server set.</summary>
    private void LoggedInAsHost(string profileId)
    {
        _cookies.GrantHost(profileId, _httpContext);
        var pairs = _httpContext.Response.Headers.SetCookie.Select(x => x!.Split(';')[0]);
        _httpContext.Request.Headers.Cookie = string.Join("; ", pairs);
    }

    [Fact]
    public async Task Delete_AnswersNoContent_AndPassesTheSessionAndTheCaller()
    {
        LoggedInAsHost("host-1");
        var id = Guid.NewGuid();

        var result = await _controller.DeleteSession(id, Ct);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal((id, new SessionCaller("host-1", false)), _sessions.Deleted.Single());
    }

    [Fact]
    public async Task Delete_WithoutAHostCookie_IsAskedAsSomeoneWhoIsNobody()
    {
        await _controller.DeleteSession(Guid.NewGuid(), Ct);

        Assert.Equal(new SessionCaller(null, false), _sessions.Deleted.Single().Caller);
    }

    [Theory]
    [InlineData(ErrorType.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound)]
    public async Task Delete_ThatTheServiceRefuses_IsTheMatchingProblem(ErrorType type, int status)
    {
        _sessions.DeleteResult = type == ErrorType.Forbidden ? Error.Forbidden(description: "no") : Error.NotFound(description: "no");

        var result = await _controller.DeleteSession(Guid.NewGuid(), Ct);

        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task RequestDeletion_AnswersNoContent_AndAsksTheServiceForTheSession()
    {
        var id = Guid.NewGuid();

        var result = await _controller.RequestDeletion(id, Ct);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal([id], _sessions.Requested);
    }

    [Fact]
    public async Task RequestDeletion_Twice_AnswersNoContentBothTimes()
    {
        var id = Guid.NewGuid();

        var first = await _controller.RequestDeletion(id, Ct);
        var second = await _controller.RequestDeletion(id, Ct);

        Assert.IsType<NoContentResult>(first);
        Assert.IsType<NoContentResult>(second);
    }

    [Fact]
    public async Task RequestDeletion_ThatTheServiceRefuses_IsTheMatchingProblem()
    {
        _sessions.RequestResult = Error.Forbidden(description: "Demo sessions end on their own and cannot be deleted");

        var result = await _controller.RequestDeletion(Guid.NewGuid(), Ct);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task Cancel_AnswersNoContent_AndPassesTheSession()
    {
        var id = Guid.NewGuid();

        var result = await _adminController.CancelSessionDeletion(id, Ct);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal([id], _admin.Cancelled);
    }

    [Fact]
    public async Task Cancel_OfAMissingSession_IsNotFound()
    {
        _admin.CancelResult = Error.NotFound(description: "The session was not found");

        var result = await _adminController.CancelSessionDeletion(Guid.NewGuid(), Ct);

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public void Delete_NeedsAccessToTheSession_OnTheSessionRoute()
    {
        var method = typeof(SessionsController).GetMethod(nameof(SessionsController.DeleteSession))!;

        Assert.NotNull(method.GetCustomAttribute<RequireSessionAccessAttribute>());
        Assert.IsType<HttpDeleteAttribute>(method.GetCustomAttributes<HttpMethodAttribute>().Single());
        Assert.Equal("sessions/{sessionId:guid}", method.GetCustomAttributes<HttpMethodAttribute>().Single().Template);
        Assert.Null(method.GetCustomAttribute<EnableRateLimitingAttribute>());
    }

    [Fact]
    public void TheRequest_NeedsAccessToTheSession_AndIsRateLimitedPerIp()
    {
        var method = typeof(SessionsController).GetMethod(nameof(SessionsController.RequestDeletion))!;

        Assert.NotNull(method.GetCustomAttribute<RequireSessionAccessAttribute>());
        Assert.IsType<HttpPostAttribute>(method.GetCustomAttributes<HttpMethodAttribute>().Single());
        Assert.Equal("sessions/{sessionId:guid}/deletion-request", method.GetCustomAttributes<HttpMethodAttribute>().Single().Template);
        Assert.Equal(
            SessionsController.DeletionRequestRateLimitPolicy,
            method.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
    }

    [Fact]
    public void TheCancel_IsForAdminsOnly_AndNotForSessionAccess()
    {
        var method = typeof(AdminController).GetMethod(nameof(AdminController.CancelSessionDeletion))!;

        Assert.NotNull(method.GetCustomAttribute<AuthorizeAdminAttribute>());
        Assert.Null(method.GetCustomAttribute<RequireSessionAccessAttribute>());
        Assert.IsType<HttpDeleteAttribute>(method.GetCustomAttributes<HttpMethodAttribute>().Single());
        Assert.Equal(
            "admin/sessions/{sessionId:guid}/deletion-request",
            method.GetCustomAttributes<HttpMethodAttribute>().Single().Template);
    }

    private sealed class FakeSessionService : ISessionService
    {
        public List<(Guid Id, SessionCaller Caller)> Deleted { get; } = [];
        public List<Guid> Requested { get; } = [];
        public ErrorOr<Success> DeleteResult { get; set; } = Result.Success;
        public ErrorOr<Success> RequestResult { get; set; } = Result.Success;

        public Task<ErrorOr<Success>> DeleteAsync(Guid id, SessionCaller caller, CancellationToken cancellationToken)
        {
            Deleted.Add((id, caller));

            return Task.FromResult(DeleteResult);
        }

        public Task<ErrorOr<Success>> RequestDeletionAsync(Guid id, CancellationToken cancellationToken)
        {
            Requested.Add(id);

            return Task.FromResult(RequestResult);
        }

        public Task<IReadOnlyList<SessionListItemDto>> GetAllAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ErrorOr<SessionDto>> GetAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ErrorOr<Success>> RenameAsync(
            Guid id,
            string? name,
            SessionCaller caller,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeSessionAdminService : ISessionAdminService
    {
        public List<Guid> Cancelled { get; } = [];
        public ErrorOr<Success> CancelResult { get; set; } = Result.Success;

        public Task<ErrorOr<Success>> CancelDeletionAsync(Guid id, CancellationToken cancellationToken)
        {
            Cancelled.Add(id);

            return Task.FromResult(CancelResult);
        }

        public Task<IReadOnlyList<SessionDto>> GetAllAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ErrorOr<SessionDto>> CreateAsync(string? name, string? password, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ErrorOr<Success>> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ErrorOr<Success>> ReleaseAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
