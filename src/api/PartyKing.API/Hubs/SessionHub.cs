using PartyKing.API.Cookies;
using Microsoft.AspNetCore.SignalR;
using PartyKing.API.Authentication;
using PartyKing.Application.Games.Services.Resistance;
using PartyKing.Application.Realtime;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.Slideshow.Services;
using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.API.Hubs;

/// <summary>
/// Live music and slideshow updates for one unlocked session. Clients connect with <c>?sessionId=</c> and the
/// session-access cookie, plus <c>&topics=</c> to receive only some pushes (see <see cref="SessionHubTopic"/>); mutations
/// stay on the REST controllers. The only hub methods tell the server that the
/// host or an admin displays the slideshow, so its timer runs. A connection with <c>&presence=1</c> belongs to the
/// Resistance player in the signed player cookie (the player's own page; the TV view leaves the flag out, because
/// it shares that cookie) and counts as that player being online while it lasts.
/// </summary>
public class SessionHub : Hub
{
    public const string Path = "/hubs/session";

    public const string PresenceQuery = "presence";

    private const string SessionIdItem = "sessionId";

    private readonly ISessionAccessService _accessService;
    private readonly ISignedCookies _cookies;
    private readonly ISlideshowService _slideshowService;
    private readonly IGamePresence _presence;
    private readonly IResistanceSeatService _seats;

    public SessionHub(
        ISessionAccessService accessService,
        ISignedCookies cookies,
        ISlideshowService slideshowService,
        IGamePresence presence,
        IResistanceSeatService seats)
    {
        _accessService = accessService;
        _cookies = cookies;
        _slideshowService = slideshowService;
        _presence = presence;
        _seats = seats;
    }

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext()
            ?? throw new HubException("HTTP context is required");

        if (!Guid.TryParse(http.Request.Query["sessionId"], out var sessionId)
            || !_cookies.TryReadSessionAccess(http.Request, sessionId, out var stamp)
            || !await _accessService.HasAccessAsync(sessionId, stamp, Context.ConnectionAborted))
        {
            Context.Abort();
            return;
        }

        if (!SessionHubTopics.TryParse(http.Request.Query["topics"], out var topics))
        {
            Context.Abort();
            return;
        }

        Context.Items[SessionIdItem] = sessionId;

        foreach (var topic in topics)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId, topic), Context.ConnectionAborted);
        }

        if (http.Request.Query[PresenceQuery] == "1"
            && _cookies.TryReadGamePlayer(GameKind.Resistance, http.Request, sessionId, out var seat)
            && await _seats.HoldsSeatAsync(sessionId, seat, Context.ConnectionAborted))
        {
            // The player comes from the cookie, and only while the seat is still theirs: a device whose seat was taken
            // over must not keep it online
            await _presence.ConnectAsync(GameKind.Resistance, sessionId, seat.PlayerId, Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _slideshowService.Unwatch(Context.ConnectionId);
        _presence.Disconnect(Context.ConnectionId);

        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>The caller displays the slideshow. Only the session's host or an admin may, as it drives the timer.</summary>
    public async Task WatchSlideshow()
    {
        var http = Context.GetHttpContext()
            ?? throw new HubException("HTTP context is required");
        var sessionId = (Guid)Context.Items[SessionIdItem]!;

        var caller = await http.ReadSessionCallerAsync(_cookies);

        var watching = await _slideshowService.WatchAsync(
            sessionId,
            caller,
            Context.ConnectionId,
            Context.ConnectionAborted);
        if (watching.IsError)
        {
            throw new HubException("Only the host or an admin can watch the slideshow");
        }
    }

    public void StopWatchingSlideshow()
    {
        _slideshowService.Unwatch(Context.ConnectionId);
    }

    internal static string GroupName(Guid sessionId, SessionHubTopic topic) => $"session:{sessionId}:{topic.ToString().ToLowerInvariant()}";
}
