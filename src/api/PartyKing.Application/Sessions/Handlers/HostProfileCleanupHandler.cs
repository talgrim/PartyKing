using PartyKing.Application.Events;
using PartyKing.Application.Ports;
using PartyKing.Domain.Events.Sessions;

namespace PartyKing.Application.Sessions.Handlers;

/// <summary>
/// Forgets the host's Spotify profile (display name and refresh token) when the session is deleted. An account hosts
/// one session at a time (unique index), so this is the last session the profile served. The host logs in again to
/// host a new one.
/// </summary>
internal sealed class HostProfileCleanupHandler : IDomainEventHandler<SessionDeleted>
{
    private readonly ISpotifyProfilesRepository _profilesRepository;

    public HostProfileCleanupHandler(ISpotifyProfilesRepository profilesRepository)
    {
        _profilesRepository = profilesRepository;
    }

    public async Task HandleAsync(SessionDeleted domainEvent, CancellationToken cancellationToken)
    {
        if (domainEvent.HostProfileId is null)
        {
            return;
        }

        await _profilesRepository.DeleteProfileAsync(domainEvent.HostProfileId, cancellationToken);
    }
}
