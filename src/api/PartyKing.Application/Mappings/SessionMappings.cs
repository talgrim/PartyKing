using PartyKing.Contract.V1.Sessions;
using PartyKing.Domain.Entities.Sessions;

namespace PartyKing.Application.Mappings;

public static class SessionMappings
{
    public static SessionDto ToDto(this Session session)
    {
        return new SessionDto(
            session.Id,
            session.Code,
            session.Name,
            session.HostProfile?.DisplayName ?? session.HostProfileId,
            session.HostProfileId is not null,
            session.FallbackPlaylistName,
            session.FallbackShuffle);
    }
}
