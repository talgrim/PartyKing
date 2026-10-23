using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PartyKing.Application.Sessions.Services;
using PartyKing.Infrastructure.Authorization;

namespace PartyKing.API.Authorization;

/// <summary>
/// Requires that the browser has entered the password of the session named by the route's <c>sessionId</c>.
/// Admins are not exempt: they join with the password like everyone else.
/// </summary>
public class RequireSessionAccessAttribute() : TypeFilterAttribute(typeof(SessionAccessFilter));

internal class SessionAccessFilter : IAsyncAuthorizationFilter
{
    private readonly ISessionService _sessionService;
    private readonly ICookieAuthorizationHandler _cookieAuthorizationHandler;

    public SessionAccessFilter(ISessionService sessionService, ICookieAuthorizationHandler cookieAuthorizationHandler)
    {
        _sessionService = sessionService;
        _cookieAuthorizationHandler = cookieAuthorizationHandler;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var hasAccess = Guid.TryParse(context.RouteData.Values["sessionId"]?.ToString(), out var sessionId)
            && _cookieAuthorizationHandler.TryReadSessionAccess(context.HttpContext.Request, sessionId, out var stamp)
            && await _sessionService.HasAccessAsync(sessionId, stamp, context.HttpContext.RequestAborted);

        if (!hasAccess)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "The session password is required"
            })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
        }
    }
}
