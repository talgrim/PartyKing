using System.Collections.Concurrent;
using PartyKing.Application.System;
using PartyKing.Domain.Entities.Lyrics;

namespace PartyKing.Application.Lyrics.Services;

/// <summary>
/// Remembers lyrics (or that there are none) per song, so a song played again, or watched by the host and an admin,
/// does not search LRCLIB again.
/// </summary>
public interface ILyricsCache
{
    /// <summary>True when the song was searched recently; <paramref name="lines"/> is null for "no lyrics found".</summary>
    bool TryGet(string key, out IReadOnlyList<LyricsLine>? lines);

    void Set(string key, IReadOnlyList<LyricsLine>? lines);
}

public sealed class LyricsCache : ILyricsCache
{
    // Lyrics don't change; a miss may be fixed by someone adding them to LRCLIB, so look again after a while
    private static readonly TimeSpan FoundLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan MissingLifetime = TimeSpan.FromHours(1);

    private readonly IDateTimeProvider _clock;
    private readonly ConcurrentDictionary<string, (DateTimeOffset ExpiresAt, IReadOnlyList<LyricsLine>? Lines)> _entries = new();

    public LyricsCache(IDateTimeProvider clock)
    {
        _clock = clock;
    }

    public bool TryGet(string key, out IReadOnlyList<LyricsLine>? lines)
    {
        if (_entries.TryGetValue(key, out var entry) && entry.ExpiresAt > _clock.UtcNow)
        {
            lines = entry.Lines;
            return true;
        }

        lines = null;
        return false;
    }

    public void Set(string key, IReadOnlyList<LyricsLine>? lines)
    {
        _entries[key] = (_clock.UtcNow.Add(lines is null ? MissingLifetime : FoundLifetime), lines);
    }
}
