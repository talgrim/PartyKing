using Microsoft.AspNetCore.SignalR;
using PartyKing.Application.Sessions.Services;
using PartyKing.Infrastructure.Authorization;

namespace PartyKing.API.Hubs;

/// <summary>
/// Live music updates for one unlocked session. Clients connect with <c>?sessionId=</c> and the session-access
/// cookie; mutations stay on the REST controllers.
/// </summary>
public class SessionHub : Hub
{
    public const string Path = "/hubs/session";

    private readonly ISessionService _sessionService;
    private readonly ICookieAuthorizationHandler _cookieAuthorizationHandler;

    public SessionHub(ISessionService sessionService, ICookieAuthorizationHandler cookieAuthorizationHandler)
    {
        _sessionService = sessionService;
        _cookieAuthorizationHandler = cookieAuthorizationHandler;
    }

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext()
            ?? throw new HubException("HTTP context is required");

        if (!Guid.TryParse(http.Request.Query["sessionId"], out var sessionId)
            || !_cookieAuthorizationHandler.TryReadSessionAccess(http.Request, sessionId, out var stamp)
            || !await _sessionService.HasAccessAsync(sessionId, stamp, Context.ConnectionAborted))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId), Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    internal static string GroupName(Guid sessionId) => $"session:{sessionId}";
}
