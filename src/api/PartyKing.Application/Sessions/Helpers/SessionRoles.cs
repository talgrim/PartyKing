using PartyKing.Domain.Entities.Sessions;

namespace PartyKing.Application.Sessions.Helpers;

internal static class SessionRoles
{
    public static bool IsHost(Session session, SessionCaller caller)
    {
        return caller.ProfileId is not null && session.HostProfileId == caller.ProfileId;
    }

    public static bool IsManager(Session session, SessionCaller caller)
    {
        return caller.IsAdmin || IsHost(session, caller);
    }
}
