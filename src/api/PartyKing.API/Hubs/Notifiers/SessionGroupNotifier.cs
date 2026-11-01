using Microsoft.AspNetCore.SignalR;

namespace PartyKing.API.Hubs.Notifiers;

/// <summary>Base of the SignalR notifiers: every push goes to the group of the session's connections.</summary>
internal abstract class SessionGroupNotifier
{
    private readonly IHubContext<SessionHub> _hub;

    protected SessionGroupNotifier(IHubContext<SessionHub> hub)
    {
        _hub = hub;
    }

    protected IClientProxy Clients(Guid sessionId)
    {
        return _hub.Clients.Group(SessionHub.GroupName(sessionId));
    }
}
