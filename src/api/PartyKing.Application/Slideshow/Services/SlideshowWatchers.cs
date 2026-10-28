namespace PartyKing.Application.Slideshow.Services;

/// <summary>Tracks which hub connections currently display a session's slideshow.</summary>
internal interface ISlideshowWatchers
{
    /// <summary>Registers the connection as a watcher of the session and returns how many watch it now.</summary>
    int Watch(Guid sessionId, string connectionId);

    /// <summary>Removes the connection. Returns the session it watched and how many watchers are left, or null.</summary>
    (Guid SessionId, int Count)? Unwatch(string connectionId);

    bool IsWatched(Guid sessionId);
}

internal sealed class SlideshowWatchers : ISlideshowWatchers
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Guid> _sessionByConnection = new();

    public int Watch(Guid sessionId, string connectionId)
    {
        lock (_gate)
        {
            _sessionByConnection[connectionId] = sessionId;

            return Count(sessionId);
        }
    }

    public (Guid SessionId, int Count)? Unwatch(string connectionId)
    {
        lock (_gate)
        {
            if (!_sessionByConnection.Remove(connectionId, out var sessionId))
            {
                return null;
            }

            return (sessionId, Count(sessionId));
        }
    }

    public bool IsWatched(Guid sessionId)
    {
        lock (_gate)
        {
            return Count(sessionId) > 0;
        }
    }

    private int Count(Guid sessionId) => _sessionByConnection.Values.Count(id => id == sessionId);
}
