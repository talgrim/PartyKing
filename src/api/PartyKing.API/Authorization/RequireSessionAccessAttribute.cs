using PartyKing.Application.Ports;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PartyKing.API.Metrics;
using PartyKing.Application.Sessions.Services;

namespace PartyKing.API.Authorization;

/// <summary>
/// Requires that the browser has entered the password of the session named by the route's <c>sessionId</c>.
/// Admins are not exempt: they join with the password like everyone else.
/// </summary>
public class RequireSessionAccessAttribute() : TypeFilterAttribute(typeof(SessionAccessFilter));

internal class SessionAccessFilter : IAsyncAuthorizationFilter
{
    private readonly ISessionAccessService _accessService;
    private readonly ICookieAuthorizationHandler _cookieAuthorizationHandler;
    private readonly AuthorizationMetrics _metrics;

    public SessionAccessFilter(ISessionAccessService accessService, ICookieAuthorizationHandler cookieAuthorizationHandler,
        AuthorizationMetrics metrics)
    {
        _accessService = accessService;
        _cookieAuthorizationHandler = cookieAuthorizationHandler;
        _metrics = metrics;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var hasAccess = Guid.TryParse(context.RouteData.Values["sessionId"]?.ToString(), out var sessionId)
            && _cookieAuthorizationHandler.TryReadSessionAccess(context.HttpContext.Request, sessionId, out var stamp)
            && await _accessService.HasAccessAsync(sessionId, stamp, context.HttpContext.RequestAborted);

        var durationMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        _metrics.RecordDuration("RequireSessionAccess", durationMs, hasAccess);

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
