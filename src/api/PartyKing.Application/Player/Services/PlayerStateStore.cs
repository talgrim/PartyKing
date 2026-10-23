using System.Collections.Concurrent;
using PartyKing.Application.System;
using PartyKing.Contract.V1.Enums;

namespace PartyKing.Application.Player.Services;

/// <param name="Connected">The host's player reported in recently.</param>
/// <param name="SongId">The song the player said it is playing.</param>
public record PlayerSnapshot(bool Connected, int? SongId, bool Paused);

/// <summary>
/// What the host's player (a browser tab) last told the server. Nothing is persisted: after a restart the player
/// simply reports again.
/// </summary>
public interface IPlayerStateStore
{
    PlayerSnapshot Get(Guid sessionId);

    /// <summary>Records a report from the host's player and hands back the command waiting for it, once.</summary>
    PlayerCommand? Report(Guid sessionId, int? songId, bool paused);

    /// <summary>Queues a command for the player. False when no player is connected to receive it.</summary>
    bool TrySendCommand(Guid sessionId, PlayerCommand command);

    /// <summary>Forgets the session's player, e.g. when the session is deleted or its host released.</summary>
    void Remove(Guid sessionId);
}

public sealed class PlayerStateStore : IPlayerStateStore
{
    /// <summary>The player reports every couple of seconds, so a few missed reports mean it is gone.</summary>
    public static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(10);

    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ConcurrentDictionary<Guid, State> _states = new();

    public PlayerStateStore(IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    public PlayerSnapshot Get(Guid sessionId)
    {
        if (!_states.TryGetValue(sessionId, out var state))
        {
            return new PlayerSnapshot(false, null, false);
        }

        lock (state)
        {
            return new PlayerSnapshot(IsConnected(state), state.SongId, state.Paused);
        }
    }

    public PlayerCommand? Report(Guid sessionId, int? songId, bool paused)
    {
        var state = _states.GetOrAdd(sessionId, _ => new State());

        lock (state)
        {
            state.LastSeen = _dateTimeProvider.UtcNow;
            state.SongId = songId;
            state.Paused = paused;

            var command = state.PendingCommand;
            state.PendingCommand = null;

            return command;
        }
    }

    public bool TrySendCommand(Guid sessionId, PlayerCommand command)
    {
        if (!_states.TryGetValue(sessionId, out var state))
        {
            return false;
        }

        lock (state)
        {
            if (!IsConnected(state))
            {
                return false;
            }

            state.PendingCommand = command;
            return true;
        }
    }

    public void Remove(Guid sessionId)
    {
        _states.TryRemove(sessionId, out _);
    }

    private bool IsConnected(State state)
    {
        return state.LastSeen is { } lastSeen && _dateTimeProvider.UtcNow - lastSeen < ConnectionTimeout;
    }

    private sealed class State
    {
        public DateTimeOffset? LastSeen { get; set; }
        public int? SongId { get; set; }
        public bool Paused { get; set; }
        public PlayerCommand? PendingCommand { get; set; }
    }
}
