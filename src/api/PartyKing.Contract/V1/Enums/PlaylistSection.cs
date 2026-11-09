namespace PartyKing.Contract.V1.Enums;

/// <summary>A group of a session's songs that can go into a saved playlist. The groups do not overlap.</summary>
public enum PlaylistSection
{
    /// <summary>Songs that were played already, not from the fallback playlist.</summary>
    History,

    /// <summary>The song that is playing now, unless it comes from the fallback playlist.</summary>
    Current,

    /// <summary>Songs waiting to be played, not from the fallback playlist.</summary>
    Queue,

    /// <summary>Songs the server queued from the session's fallback playlist, whatever their state.</summary>
    Fallback
}
