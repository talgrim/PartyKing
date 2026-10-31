using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.SignalR;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.Slideshow.Services;
using PartyKing.Infrastructure.Authentication;
using PartyKing.Infrastructure.Authorization;

namespace PartyKing.API.Hubs;

/// <summary>
/// Live music and slideshow updates for one unlocked session. Clients connect with <c>?sessionId=</c> and the
/// session-access cookie; mutations stay on the REST controllers. The only hub methods tell the server that the
/// host or an admin displays the slideshow, so its timer runs.
/// </summary>
public class SessionHub : Hub
{
    public const string Path = "/hubs/session";

    private const string SessionIdItem = "sessionId";

    private readonly ISessionAccessService _accessService;
    private readonly ISessionAuthorizer _sessionAuthorizer;
    private readonly ICookieAuthorizationHandler _cookieAuthorizationHandler;
    private readonly ISlideshowService _slideshowService;

    public SessionHub(
        ISessionAccessService accessService,
        ISessionAuthorizer sessionAuthorizer,
        ICookieAuthorizationHandler cookieAuthorizationHandler,
        ISlideshowService slideshowService)
    {
        _accessService = accessService;
        _sessionAuthorizer = sessionAuthorizer;
        _cookieAuthorizationHandler = cookieAuthorizationHandler;
        _slideshowService = slideshowService;
    }

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext()
            ?? throw new HubException("HTTP context is required");

        if (!Guid.TryParse(http.Request.Query["sessionId"], out var sessionId)
            || !_cookieAuthorizationHandler.TryReadSessionAccess(http.Request, sessionId, out var stamp)
            || !await _accessService.HasAccessAsync(sessionId, stamp, Context.ConnectionAborted))
        {
            Context.Abort();
            return;
        }

        Context.Items[SessionIdItem] = sessionId;

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId), Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _slideshowService.Unwatch(Context.ConnectionId);

        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>The caller displays the slideshow. Only the session's host or an admin may, as it drives the timer.</summary>
    public async Task WatchSlideshow()
    {
        var http = Context.GetHttpContext()
            ?? throw new HubException("HTTP context is required");
        var sessionId = (Guid)Context.Items[SessionIdItem]!;

        var profileId = _cookieAuthorizationHandler.TryReadProfileId(http.Request, out var id) ? id : null;
        var isAdmin = (await http.AuthenticateAsync(Constants.AdminScheme)).Succeeded;

        if ((await _sessionAuthorizer.AuthorizeManagerAsync(sessionId, profileId, isAdmin, Context.ConnectionAborted)).IsError)
        {
            throw new HubException("Only the host or an admin can watch the slideshow");
        }

        await _slideshowService.WatchAsync(sessionId, Context.ConnectionId, Context.ConnectionAborted);
    }

    public void StopWatchingSlideshow()
    {
        _slideshowService.Unwatch(Context.ConnectionId);
    }

    internal static string GroupName(Guid sessionId) => $"session:{sessionId}";
}
