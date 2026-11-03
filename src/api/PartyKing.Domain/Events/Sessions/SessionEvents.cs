namespace PartyKing.Domain.Events.Sessions;

/// <summary>The session got a new name (already trimmed).</summary>
public sealed record SessionRenamed(Guid SessionId, string Name) : IDomainEvent;

/// <summary>The password changed, so every access stamp issued before it stopped working. The hash is not carried.</summary>
public sealed record SessionPasswordChanged(Guid SessionId) : IDomainEvent;

/// <summary>An account became the session's host. The host logging in again raises nothing.</summary>
public sealed record HostClaimed(Guid SessionId, string ProfileId) : IDomainEvent;

/// <summary>
/// The host was removed (<paramref name="PreviousHostProfileId"/> is null when there was none, the cleanup still runs).
/// A fallback playlist the host had chosen goes with them, announced by <see cref="FallbackPlaylistCleared"/>.
/// </summary>
public sealed record HostReleased(Guid SessionId, string? PreviousHostProfileId) : IDomainEvent;

/// <summary>
/// The session is gone, its queue and games with it. Carries what the cleanup needs, because the row can no longer
/// be read: the host whose Spotify token is dropped and the playlist whose cached tracks are.
/// </summary>
public sealed record SessionDeleted(Guid SessionId, string? HostProfileId, string? FallbackPlaylistId) : IDomainEvent;

/// <summary>A fallback playlist was set (or saved again, e.g. to change the shuffle flag) and rewound to its start.</summary>
public sealed record FallbackPlaylistChanged(Guid SessionId, string? PreviousPlaylistId, string PlaylistId) : IDomainEvent;

/// <summary>The fallback playlist was removed. Never raised when the session had none.</summary>
public sealed record FallbackPlaylistCleared(Guid SessionId, string PreviousPlaylistId) : IDomainEvent;
