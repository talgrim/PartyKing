using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PartyKing.Application.Player.Services;
using PartyKing.Contract.V1.Player;

namespace PartyKing.Application.Realtime;

internal sealed class PlayerConnectionWatchdog : IPlayerConnectionWatchdog, IAsyncDisposable
{
    private readonly IPlayerStateStore _playerStateStore;
    private readonly IPlayerNotifier _notifier;
    private readonly ILogger<PlayerConnectionWatchdog> _logger;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _arms = new();

    public PlayerConnectionWatchdog(
        IPlayerStateStore playerStateStore,
        IPlayerNotifier notifier,
        ILogger<PlayerConnectionWatchdog> logger)
    {
        _playerStateStore = playerStateStore;
        _notifier = notifier;
        _logger = logger;
    }

    public void Arm(Guid sessionId, int? songId, bool paused)
    {
        var cts = new CancellationTokenSource();
        if (_arms.TryRemove(sessionId, out var previous))
        {
            previous.Cancel();
            previous.Dispose();
        }

        _arms[sessionId] = cts;
        _ = NotifyIfDisconnectedAsync(sessionId, songId, paused, cts.Token);
    }

    public void Disarm(Guid sessionId)
    {
        if (_arms.TryRemove(sessionId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sessionId in _arms.Keys.ToArray())
        {
            Disarm(sessionId);
        }

        await Task.CompletedTask;
    }

    private async Task NotifyIfDisconnectedAsync(
        Guid sessionId,
        int? songId,
        bool paused,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(PlayerStateStore.ConnectionTimeout, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_playerStateStore.Get(sessionId).Connected)
        {
            return;
        }

        _logger.LogWarning("The player of session {SessionId} stopped reporting and is treated as gone", sessionId);

        await _notifier.PlayerStateChangedAsync(
            sessionId,
            new PlayerStateChangedDto(songId, paused, PlayerConnected: false),
            CancellationToken.None);
    }
}
