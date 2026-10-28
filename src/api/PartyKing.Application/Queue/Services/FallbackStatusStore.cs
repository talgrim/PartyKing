using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PartyKing.Application.Realtime;
using PartyKing.Contract.V1.Player;

namespace PartyKing.Application.Queue.Services;

/// <summary>
/// Remembers per session why the fallback playlist could not add a song, so the host's player can show it. A change
/// is also pushed to the session, so the player doesn't have to wait for its next report.
/// Nothing is persisted: the next refill sets or clears it again.
/// </summary>
public interface IFallbackStatusStore
{
    string? GetError(Guid sessionId);
    void SetError(Guid sessionId, string error);
    void Clear(Guid sessionId);
}

public sealed class FallbackStatusStore : IFallbackStatusStore
{
    private readonly ConcurrentDictionary<Guid, string> _errors = new();
    private readonly ISessionMusicNotifier _notifier;
    private readonly ILogger<FallbackStatusStore> _logger;

    public FallbackStatusStore(ISessionMusicNotifier notifier, ILogger<FallbackStatusStore> logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    public string? GetError(Guid sessionId) => _errors.GetValueOrDefault(sessionId);

    public void SetError(Guid sessionId, string error)
    {
        var previous = _errors.TryGetValue(sessionId, out var current) ? current : null;
        _errors[sessionId] = error;

        if (previous != error)
        {
            _logger.LogWarning("Fallback playlist of session {SessionId}: {Error}", sessionId, error);
            Notify(sessionId, error);
        }
    }

    public void Clear(Guid sessionId)
    {
        if (_errors.TryRemove(sessionId, out _))
        {
            Notify(sessionId, null);
        }
    }

    private void Notify(Guid sessionId, string? error)
    {
        _ = PushAsync(sessionId, error);
    }

    private async Task PushAsync(Guid sessionId, string? error)
    {
        try
        {
            await _notifier.FallbackErrorChangedAsync(sessionId, new FallbackErrorDto(error), CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not push the fallback playlist error of session {SessionId}", sessionId);
        }
    }
}
