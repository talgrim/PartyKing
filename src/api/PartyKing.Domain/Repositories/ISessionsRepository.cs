using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Enums;

namespace PartyKing.Domain.Repositories;

public interface ISessionsRepository
{
    /// <summary>Returns false when the session code is already taken.</summary>
    Task<bool> TryAddAsync(Session session, CancellationToken cancellationToken);
    Task<IReadOnlyList<Session>> GetAllAsync(CancellationToken cancellationToken);
    Task<Session?> GetAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>Renames the session; the name rule is the aggregate's, so a refused name is part of the result.</summary>
    Task<SessionRenameResult> RenameAsync(Guid id, string? name, CancellationToken cancellationToken);
    Task<bool> UpdatePasswordHashAsync(Guid id, string passwordHash, CancellationToken cancellationToken);

    /// <summary>Sets the fallback playlist and rewinds it; a null <paramref name="playlistId"/> clears it.</summary>
    Task<bool> SetFallbackPlaylistAsync(
        Guid id,
        string? playlistId,
        string? playlistName,
        bool shuffle,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stores the cursor, but only while the session still has <paramref name="playlistId"/> and is at
    /// <paramref name="expectedCursor"/>, the values the caller's pick was based on: a playlist that was changed or
    /// rewound meanwhile keeps its new cursor. Returns whether the cursor was stored.
    /// </summary>
    Task<bool> SetFallbackCursorAsync(
        Guid id,
        string playlistId,
        int expectedCursor,
        int cursor,
        CancellationToken cancellationToken);

    /// <summary>
    /// Makes a saved playlist the fallback and rewinds it, replacing a Spotify one. The caller has checked that the
    /// playlist exists and belongs to the account that chose it.
    /// </summary>
    Task<bool> SetFallbackSavedPlaylistAsync(
        Guid id,
        Guid savedPlaylistId,
        string playlistName,
        bool shuffle,
        CancellationToken cancellationToken);

    /// <summary>
    /// Like <see cref="SetFallbackCursorAsync"/> for a saved playlist: stores the cursor only while the session still
    /// has <paramref name="savedPlaylistId"/> and is at <paramref name="expectedCursor"/>.
    /// </summary>
    Task<bool> SetFallbackSavedCursorAsync(
        Guid id,
        Guid savedPlaylistId,
        int expectedCursor,
        int cursor,
        CancellationToken cancellationToken);

    /// <summary>Deletes the session together with its queue.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Atomically assigns the host. The first profile to claim a session owns it.</summary>
    Task<SessionClaimResult> TryClaimAsync(Guid id, string profileId, CancellationToken cancellationToken);
    Task<bool> ReleaseAsync(Guid id, CancellationToken cancellationToken);
}
