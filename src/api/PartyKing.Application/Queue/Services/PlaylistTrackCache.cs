using System.Collections.Concurrent;
using PartyKing.Application.System;
using PartyKing.Domain.Entities.Spotify;

namespace PartyKing.Application.Queue.Services;

/// <summary>Keeps the tracks of fallback playlists for a few minutes, so every refill doesn't re-read the playlist.</summary>
public interface IPlaylistTrackCache
{
    bool TryGet(string playlistId, out IReadOnlyList<Song> tracks);
    void Set(string playlistId, IReadOnlyList<Song> tracks);
    void Remove(string playlistId);
}

public sealed class PlaylistTrackCache : IPlaylistTrackCache
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly IDateTimeProvider _clock;
    private readonly ConcurrentDictionary<string, (DateTimeOffset ExpiresAt, IReadOnlyList<Song> Tracks)> _entries = new();

    public PlaylistTrackCache(IDateTimeProvider clock)
    {
        _clock = clock;
    }

    public bool TryGet(string playlistId, out IReadOnlyList<Song> tracks)
    {
        if (_entries.TryGetValue(playlistId, out var entry) && entry.ExpiresAt > _clock.UtcNow)
        {
            tracks = entry.Tracks;
            return true;
        }

        tracks = [];
        return false;
    }

    public void Set(string playlistId, IReadOnlyList<Song> tracks)
    {
        _entries[playlistId] = (_clock.UtcNow.Add(Lifetime), tracks);
    }

    public void Remove(string playlistId) => _entries.TryRemove(playlistId, out _);
}
