using Microsoft.AspNetCore.SignalR;

namespace PartyKing.API.Hubs.Notifiers;

/// <summary>Base of the SignalR notifiers: every push goes to the connections of the session that follow its topic.</summary>
internal abstract class SessionGroupNotifier
{
    private readonly IHubContext<SessionHub> _hub;
    private readonly SessionHubTopic _topic;

    protected SessionGroupNotifier(IHubContext<SessionHub> hub, SessionHubTopic topic)
    {
        _hub = hub;
        _topic = topic;
    }

    protected IClientProxy Clients(Guid sessionId)
    {
        return _hub.Clients.Group(SessionHub.GroupName(sessionId, _topic));
    }
}
