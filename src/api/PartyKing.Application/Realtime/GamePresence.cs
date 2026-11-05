using Microsoft.Extensions.Logging;
using PartyKing.Contract.V1.Games.Resistance;
using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.Application.Realtime;

/// <summary>
/// Which players of a game have a device connected right now. The hub registers a connection that belongs to a
/// player (the player id comes from the signed cookie, never from the client) and removes it when it closes.
/// </summary>
public interface IGamePresence
{
    /// <summary>Registers the connection as the player's. Pushes the new list when the player was offline.</summary>
    Task ConnectAsync(GameKind kind, Guid sessionId, Guid playerId, string connectionId);

    /// <summary>
    /// Removes the connection. The player stays online for the grace period, so a reconnect or a page change
    /// does not flicker; only then is the new list pushed.
    /// </summary>
    void Disconnect(string connectionId);

    /// <summary>The players that are online, the ones inside their grace period included.</summary>
    IReadOnlyList<Guid> OnlinePlayers(GameKind kind, Guid sessionId);

    /// <summary>
    /// Drops every connection of the player at once, without a grace period, and pushes the new list when they were
    /// online. For a seat that was taken over: the connections of the device that lost it must not keep it online.
    /// </summary>
    Task ForgetAsync(GameKind kind, Guid sessionId, Guid playerId);
}

internal sealed class GamePresence : IGamePresence, IDisposable
{
    /// <summary>How long a player with no connection left is still shown online.</summary>
    public static readonly TimeSpan DefaultGrace = TimeSpan.FromSeconds(10);

    private readonly IResistanceNotifier _notifier;
    private readonly ILogger<GamePresence> _logger;
    private readonly TimeSpan _grace;
    private readonly object _gate = new();
    private readonly Dictionary<string, Key> _keyByConnection = new();
    private readonly Dictionary<Key, Entry> _entries = new();

    public GamePresence(IResistanceNotifier notifier, ILogger<GamePresence> logger, TimeSpan grace)
    {
        _notifier = notifier;
        _logger = logger;
        _grace = grace;
    }

    public async Task ConnectAsync(GameKind kind, Guid sessionId, Guid playerId, string connectionId)
    {
        var key = new Key(kind, sessionId, playerId);
        var cameOnline = false;

        lock (_gate)
        {
            if (_keyByConnection.TryGetValue(connectionId, out var previous) && previous != key)
            {
                RemoveConnection(connectionId, previous);
            }

            _keyByConnection[connectionId] = key;
            if (!_entries.TryGetValue(key, out var entry))
            {
                entry = new Entry();
                _entries[key] = entry;
            }

            entry.Connections.Add(connectionId);
            entry.CancelGrace();
            if (!entry.Online)
            {
                entry.Online = true;
                cameOnline = true;
            }
        }

        if (cameOnline)
        {
            await PushAsync(kind, sessionId);
        }
    }

    public void Disconnect(string connectionId)
    {
        lock (_gate)
        {
            if (!_keyByConnection.Remove(connectionId, out var key))
            {
                return;
            }

            RemoveConnection(connectionId, key);
        }
    }

    public IReadOnlyList<Guid> OnlinePlayers(GameKind kind, Guid sessionId)
    {
        lock (_gate)
        {
            return OnlineIds(kind, sessionId);
        }
    }

    public async Task ForgetAsync(GameKind kind, Guid sessionId, Guid playerId)
    {
        bool wasOnline;

        lock (_gate)
        {
            if (!_entries.Remove(new Key(kind, sessionId, playerId), out var entry))
            {
                return;
            }

            foreach (var connectionId in entry.Connections)
            {
                _keyByConnection.Remove(connectionId);
            }

            entry.CancelGrace();
            wasOnline = entry.Online;
        }

        if (wasOnline)
        {
            await PushAsync(kind, sessionId);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var entry in _entries.Values)
            {
                entry.CancelGrace();
            }
        }
    }

    // Called under the lock; the connection is no longer in _keyByConnection
    private void RemoveConnection(string connectionId, Key key)
    {
        if (!_entries.TryGetValue(key, out var entry))
        {
            return;
        }

        entry.Connections.Remove(connectionId);
        if (entry.Connections.Count > 0 || entry.Grace is not null)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        entry.Grace = cts;
        _ = ExpireAsync(key, entry, cts.Token);
    }

    private async Task ExpireAsync(Key key, Entry entry, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_grace, cancellationToken);

            lock (_gate)
            {
                if (cancellationToken.IsCancellationRequested || entry.Connections.Count > 0)
                {
                    return;
                }

                entry.Grace = null;
                _entries.Remove(key);
            }

            await PushAsync(key.Kind, key.SessionId);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Pushing the presence of session {SessionId} failed", key.SessionId);
        }
    }

    private Task PushAsync(GameKind kind, Guid sessionId)
    {
        // Only the Resistance TV shows presence for now; the other games track it without pushing
        if (kind != GameKind.Resistance)
        {
            return Task.CompletedTask;
        }

        return _notifier.PresenceChangedAsync(
            sessionId,
            new ResistancePresenceDto(OnlinePlayers(kind, sessionId)),
            CancellationToken.None);
    }

    private List<Guid> OnlineIds(GameKind kind, Guid sessionId)
    {
        return _entries
            .Where(x => x.Key.Kind == kind && x.Key.SessionId == sessionId && x.Value.Online)
            .Select(x => x.Key.PlayerId)
            .ToList();
    }

    private readonly record struct Key(GameKind Kind, Guid SessionId, Guid PlayerId);

    private sealed class Entry
    {
        public HashSet<string> Connections { get; } = [];

        public bool Online { get; set; }

        public CancellationTokenSource? Grace { get; set; }

        public void CancelGrace()
        {
            Grace?.Cancel();
            Grace?.Dispose();
            Grace = null;
        }
    }
}
