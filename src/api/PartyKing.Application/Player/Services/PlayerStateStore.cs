using System.Collections.Concurrent;
using PartyKing.Application.System;
using PartyKing.Contract.V1.Enums;

namespace PartyKing.Application.Player.Services;

/// <param name="Connected">The host's player reported in recently.</param>
/// <param name="SongId">The song the player said it is playing.</param>
/// <param name="PositionMs">Where in the song the player is now: the last reported position, moved on by the time that has passed unless paused.</param>
public record PlayerSnapshot(bool Connected, int? SongId, bool Paused, int PositionMs = 0);

/// <param name="Command">The command an admin sent since the previous report, if any.</param>
/// <param name="Changed">The report differs from what was known: another song, pause state, a seek or a player that was gone.
/// A position that only moved on as the song plays is not a change.</param>
public record PlayerReport(PlayerCommand? Command, bool Changed);

/// <summary>
/// What the host's player (a browser tab) last told the server. Nothing is persisted: after a restart the player
/// simply reports again.
/// </summary>
public interface IPlayerStateStore
{
    PlayerSnapshot Get(Guid sessionId);

    /// <summary>
    /// Records a report from the host's player and hands back the command waiting for it, once. The position is
    /// remembered but never makes a report a change by itself: <paramref name="seeked"/> says the player jumped.
    /// </summary>
    PlayerReport Report(Guid sessionId, int? songId, bool paused, int positionMs = 0, bool seeked = false);

    /// <summary>Queues a command for the player. False when no player is connected to receive it.</summary>
    bool TrySendCommand(Guid sessionId, PlayerCommand command);

    /// <summary>Forgets the session's player, e.g. when the session is deleted or its host released.</summary>
    void Remove(Guid sessionId);
}

public sealed class PlayerStateStore : IPlayerStateStore
{
    /// <summary>The player reports every ~10 s, so a few missed reports mean it is gone.</summary>
    public static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(30);

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
            return new PlayerSnapshot(IsConnected(state), state.SongId, state.Paused, CurrentPositionMs(state));
        }
    }

    public PlayerReport Report(Guid sessionId, int? songId, bool paused, int positionMs = 0, bool seeked = false)
    {
        var state = _states.GetOrAdd(sessionId, _ => new State());

        lock (state)
        {
            var changed = !IsConnected(state) || state.SongId != songId || state.Paused != paused || seeked;

            state.LastSeen = _dateTimeProvider.UtcNow;
            state.SongId = songId;
            state.Paused = paused;
            state.PositionMs = Math.Max(0, positionMs);

            var command = state.PendingCommand;
            state.PendingCommand = null;

            return new PlayerReport(command, changed);
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

    // Called under the state's lock. LastSeen is when the position was reported.
    private int CurrentPositionMs(State state)
    {
        if (state.LastSeen is not { } reportedAt || state.Paused)
        {
            return state.PositionMs;
        }

        var elapsed = _dateTimeProvider.UtcNow - reportedAt;
        return (int)Math.Min(int.MaxValue, state.PositionMs + Math.Max(0, elapsed.TotalMilliseconds));
    }

    private sealed class State
    {
        public int PositionMs { get; set; }
        public DateTimeOffset? LastSeen { get; set; }
        public int? SongId { get; set; }
        public bool Paused { get; set; }
        public PlayerCommand? PendingCommand { get; set; }
    }
}
