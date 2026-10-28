using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PartyKing.Application.System;

namespace PartyKing.Application.Slideshow.Services;

/// <summary>Runs one callback per session at a given moment; scheduling again replaces the pending callback.</summary>
internal interface ISlideshowScheduler
{
    void Schedule(Guid sessionId, DateTimeOffset at, Func<Task> callback);

    void Cancel(Guid sessionId);
}

internal sealed class SlideshowScheduler : ISlideshowScheduler, IDisposable
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<SlideshowScheduler> _logger;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _timers = new();

    public SlideshowScheduler(IDateTimeProvider dateTimeProvider, ILogger<SlideshowScheduler> logger)
    {
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public void Schedule(Guid sessionId, DateTimeOffset at, Func<Task> callback)
    {
        var cts = new CancellationTokenSource();
        if (_timers.TryRemove(sessionId, out var previous))
        {
            previous.Cancel();
            previous.Dispose();
        }

        _timers[sessionId] = cts;
        _ = RunAsync(sessionId, at - _dateTimeProvider.UtcNow, callback, cts.Token);
    }

    public void Cancel(Guid sessionId)
    {
        if (_timers.TryRemove(sessionId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    public void Dispose()
    {
        foreach (var sessionId in _timers.Keys.ToArray())
        {
            Cancel(sessionId);
        }
    }

    private async Task RunAsync(Guid sessionId, TimeSpan delay, Func<Task> callback, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay > TimeSpan.Zero ? delay : TimeSpan.Zero, cancellationToken);
            await callback();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Rotating the slideshow of session {SessionId} failed", sessionId);
        }
    }
}
