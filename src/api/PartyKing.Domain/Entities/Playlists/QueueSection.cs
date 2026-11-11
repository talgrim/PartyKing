namespace PartyKing.Domain.Entities.Playlists;

/// <summary>
/// A group of the songs of a session that a saved playlist can take. The groups do not overlap: a song the server
/// queued from the fallback playlist belongs to <see cref="Fallback"/> only, whatever its state.
/// </summary>
public enum QueueSection
{
    /// <summary>Songs that were played already, not from the fallback playlist.</summary>
    History,

    /// <summary>The song that is playing now, unless it comes from the fallback playlist.</summary>
    Current,

    /// <summary>Songs waiting to be played, not from the fallback playlist.</summary>
    Queue,

    /// <summary>Songs the server queued from the session's fallback playlist, played, playing or waiting.</summary>
    Fallback
}
