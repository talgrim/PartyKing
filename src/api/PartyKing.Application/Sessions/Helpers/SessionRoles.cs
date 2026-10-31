using PartyKing.Domain.Entities.Sessions;

namespace PartyKing.Application.Sessions.Helpers;

internal static class SessionRoles
{
    /// <param name="profileId">The logged-in host's profile id, if any.</param>
    public static bool IsHost(Session session, string? profileId)
    {
        return profileId is not null && session.HostProfileId == profileId;
    }

    public static bool IsManager(Session session, string? profileId, bool isAdmin)
    {
        return isAdmin || IsHost(session, profileId);
    }
}
