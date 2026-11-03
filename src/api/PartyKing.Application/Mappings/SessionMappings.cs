using PartyKing.Contract.V1.Sessions;
using PartyKing.Domain.Entities.Sessions;
using Riok.Mapperly.Abstractions;

namespace PartyKing.Application.Mappings;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class SessionMappings
{
    [MapPropertyFromSource(nameof(SessionDto.HostName), Use = nameof(HostNameOf))]
    [MapPropertyFromSource(nameof(SessionDto.HasHost), Use = nameof(HasHostOf))]
    public static partial SessionDto ToDto(this Session session);

    private static string? HostNameOf(Session session) => session.HostProfile?.DisplayName ?? session.HostProfileId;

    private static bool HasHostOf(Session session) => session.HostProfileId is not null;
}
