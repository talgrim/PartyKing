using PartyKing.Domain.Entities.Queue;

namespace PartyKing.Domain.Events.Queue;

/// <summary>A song was added to the queue, by a guest or from the fallback playlist (see <see cref="QueueItem.IsFallback"/>).</summary>
public sealed record SongQueued(Guid SessionId, QueueItem Song) : IDomainEvent;

/// <summary>A song that was still waiting was deleted from the queue.</summary>
public sealed record SongRemoved(Guid SessionId, int SongId) : IDomainEvent;

/// <summary>
/// The queue moved on: <paramref name="PreviousSongId"/> (null when nothing was playing) was marked played and
/// <paramref name="NowPlaying"/> started, or null when nothing was left to start. A caller with an outdated view
/// advances nothing and raises nothing.
/// </summary>
public sealed record QueueAdvanced(Guid SessionId, int? PreviousSongId, QueueItem? NowPlaying) : IDomainEvent;
