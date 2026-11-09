namespace PartyKing.Domain.Enums;

/// <summary>Which kind of playlist a session's <c>FallbackPlaylistId</c> names.</summary>
public enum FallbackPlaylistKind
{
    /// <summary>A playlist of the host on Spotify; the id is Spotify's.</summary>
    Spotify,

    /// <summary>A playlist an account saved from a session's songs; the id is its <see cref="Guid"/>.</summary>
    Saved
}
