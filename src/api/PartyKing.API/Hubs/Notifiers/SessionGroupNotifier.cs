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
        return Clients(sessionId, _topic);
    }

    /// <summary>For a notifier that also sends to a topic of its own besides the one it was built for.</summary>
    protected IClientProxy Clients(Guid sessionId, SessionHubTopic topic)
    {
        return _hub.Clients.Group(SessionHub.GroupName(sessionId, topic));
    }
}
