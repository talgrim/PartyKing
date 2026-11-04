using PartyKing.Domain.Entities.Auth;

namespace PartyKing.Domain.Repositories;

public interface IOAuthStatesRepository
{
    /// <summary>Saves the state, and forgets the states that expired before <paramref name="now"/> (UTC) while at it.</summary>
    Task AddAsync(OAuthState state, DateTime now, CancellationToken cancellationToken);

    /// <summary>
    /// Single use: returns the state and deletes it, or null when it is unknown or somebody took it first, so a replayed
    /// state can't be used twice. An expired state is deleted too and still returned: <see cref="OAuthState.IsExpired"/>
    /// is the caller's to ask.
    /// </summary>
    Task<OAuthState?> TakeAsync(string id, CancellationToken cancellationToken);
}
