using System.Collections.Concurrent;

namespace PartyKing.Application.Queue.Services;

/// <summary>
/// Remembers per session why the fallback playlist could not add a song, so the host's player can show it.
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

    public string? GetError(Guid sessionId) => _errors.GetValueOrDefault(sessionId);

    public void SetError(Guid sessionId, string error) => _errors[sessionId] = error;

    public void Clear(Guid sessionId) => _errors.TryRemove(sessionId, out _);
}
