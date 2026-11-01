using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Enums;

namespace PartyKing.Domain.Repositories;

public interface ISessionsRepository
{
    /// <summary>Returns false when the session code is already taken.</summary>
    Task<bool> TryAddAsync(Session session, CancellationToken cancellationToken);
    Task<IReadOnlyList<Session>> GetAllAsync(CancellationToken cancellationToken);
    Task<Session?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> RenameAsync(Guid id, string name, CancellationToken cancellationToken);
    Task<bool> UpdatePasswordHashAsync(Guid id, string passwordHash, CancellationToken cancellationToken);

    /// <summary>Sets the fallback playlist and rewinds it; a null <paramref name="playlistId"/> clears it.</summary>
    Task<bool> SetFallbackPlaylistAsync(
        Guid id,
        string? playlistId,
        string? playlistName,
        bool shuffle,
        CancellationToken cancellationToken);

    Task<bool> SetFallbackCursorAsync(Guid id, int cursor, CancellationToken cancellationToken);

    /// <summary>Deletes the session together with its queue.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Atomically assigns the host. The first profile to claim a session owns it.</summary>
    Task<SessionClaimResult> TryClaimAsync(Guid id, string profileId, CancellationToken cancellationToken);
    Task<bool> ReleaseAsync(Guid id, CancellationToken cancellationToken);
}
