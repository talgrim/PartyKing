using ErrorOr;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using PartyKing.Application.Queue.Services;
using PartyKing.Application.Realtime;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.Slideshow.Services;
using PartyKing.Contract.V1.Lyrics;
using PartyKing.Contract.V1.Player;
using PartyKing.Contract.V1.Queue;
using PartyKing.Contract.V1.Slideshow;
using PartyKing.Application.Spotify.Services;
using PartyKing.Application.System;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Enums;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Application.Tests;

internal class FakeClock : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan by) => UtcNow += by;
}

internal class FakeSessionsRepository : ISessionsRepository
{
    private readonly List<Session> _sessions = [];

    public Session AddSession(string? hostProfileId = null, string passwordHash = "hash")
    {
        var session = new Session("ABC123", "Party", passwordHash) { HostProfileId = hostProfileId };
        _sessions.Add(session);
        return session;
    }

    public Task<Session?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_sessions.FirstOrDefault(x => x.Id == id));

    public Task<bool> TryAddAsync(Session session, CancellationToken cancellationToken)
    {
        _sessions.Add(session);
        return Task.FromResult(true);
    }

    public Task<bool> UpdatePasswordHashAsync(Guid id, string passwordHash, CancellationToken cancellationToken)
    {
        var session = _sessions.FirstOrDefault(x => x.Id == id);
        if (session is not null)
        {
            session.PasswordHash = passwordHash;
        }

        return Task.FromResult(session is not null);
    }

    public Task<IReadOnlyList<Session>> GetAllAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    public List<(Guid Id, string Name)> Renamed { get; } = [];

    public Task<bool> RenameAsync(Guid id, string name, CancellationToken cancellationToken)
    {
        var session = _sessions.FirstOrDefault(x => x.Id == id);
        if (session is not null)
        {
            Renamed.Add((id, name));
        }

        return Task.FromResult(session is not null);
    }
    public List<(Guid Id, string? PlaylistId, string? Name, bool Shuffle)> FallbackPlaylistSet { get; } = [];
    public List<(Guid Id, int Cursor)> FallbackCursorSet { get; } = [];

    public Task<bool> SetFallbackPlaylistAsync(
        Guid id,
        string? playlistId,
        string? playlistName,
        bool shuffle,
        CancellationToken cancellationToken)
    {
        var session = _sessions.FirstOrDefault(x => x.Id == id);
        if (session is not null)
        {
            session.FallbackPlaylistId = playlistId;
            session.FallbackPlaylistName = playlistName;
            session.FallbackShuffle = shuffle;
            session.FallbackCursor = 0;
            FallbackPlaylistSet.Add((id, playlistId, playlistName, shuffle));
        }

        return Task.FromResult(session is not null);
    }

    public Task<bool> SetFallbackCursorAsync(Guid id, int cursor, CancellationToken cancellationToken)
    {
        var session = _sessions.FirstOrDefault(x => x.Id == id);
        if (session is not null)
        {
            session.FallbackCursor = cursor;
            FallbackCursorSet.Add((id, cursor));
        }

        return Task.FromResult(session is not null);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_sessions.RemoveAll(x => x.Id == id) > 0);
    public Task<SessionClaimResult> TryClaimAsync(Guid id, string profileId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> ReleaseAsync(Guid id, CancellationToken cancellationToken)
    {
        var session = _sessions.FirstOrDefault(x => x.Id == id);
        if (session is not null)
        {
            session.HostProfileId = null;
        }

        return Task.FromResult(session is not null);
    }
}

internal class FakeHostTokenProvider : IHostTokenProvider
{
    public List<string> Invalidated { get; } = [];

    public void Invalidate(string profileId) => Invalidated.Add(profileId);

    public ErrorOr<string> SessionToken { get; set; } = "token";

    public Task<ErrorOr<string>> GetSessionAccessTokenAsync(Guid sessionId, CancellationToken cancellationToken) =>
        Task.FromResult(SessionToken);

    public Task<ErrorOr<string>> GetAccessTokenAsync(string profileId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

internal class FakeQueueRepository : IQueueRepository
{
    public QueueItem? Playing { get; set; }
    public List<(Guid SessionId, int? CurrentItemId)> AdvanceCalls { get; } = [];
    public QueueItem? AdvanceResult { get; set; }
    public List<(Guid SessionId, int ItemId)> RemoveCalls { get; } = [];
    public QueueItemRemoval RemoveResult { get; set; } = QueueItemRemoval.Removed;

    public Task<QueueItem?> GetPlayingAsync(Guid sessionId, CancellationToken cancellationToken) =>
        Task.FromResult(Playing);

    public Task<QueueItem?> AdvanceAsync(Guid sessionId, int? currentItemId, CancellationToken cancellationToken)
    {
        AdvanceCalls.Add((sessionId, currentItemId));
        return Task.FromResult(AdvanceResult);
    }

    public Task<QueueItemRemoval> RemoveAsync(Guid sessionId, int itemId, CancellationToken cancellationToken)
    {
        RemoveCalls.Add((sessionId, itemId));
        return Task.FromResult(RemoveResult);
    }

    public List<QueueItem> Pending { get; } = [];
    public List<QueueItem> Added { get; } = [];

    public Task<QueueItem?> AddIfNoneQueuedAsync(QueueItem item, CancellationToken cancellationToken)
    {
        if (Pending.Count > 0)
        {
            return Task.FromResult<QueueItem?>(null);
        }

        Added.Add(item);
        Pending.Add(item);
        return Task.FromResult<QueueItem?>(item);
    }

    public Task<IReadOnlyList<QueueItem>> GetPendingAsync(Guid sessionId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<QueueItem>>(Pending.ToList());

    public Task<QueueItem> AddAsync(QueueItem item, CancellationToken cancellationToken) => throw new NotSupportedException();
}

internal class FakeSlideshowService : ISlideshowService
{
    public List<Guid> DeletedSessions { get; } = [];

    public Task DeleteSessionAsync(Guid sessionId)
    {
        DeletedSessions.Add(sessionId);
        return Task.CompletedTask;
    }

    public Task InitializeAsync(string rootPath, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<ErrorOr<SlideshowSettingsDto>> GetSettingsAsync(Guid sessionId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<ErrorOr<Success>> UpdateSettingsAsync(
        Guid sessionId,
        SlideshowSettingsDto settings,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task UploadImagesAsync(
        Guid sessionId,
        IFormFile[] files,
        bool deleteAfterPresentation,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<SlideshowImageDto?> GetImageAsync(Guid sessionId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task WatchAsync(Guid sessionId, string connectionId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public void Unwatch(string connectionId) => throw new NotSupportedException();
}

internal class FakeSlideshowScheduler : ISlideshowScheduler
{
    public Dictionary<Guid, (DateTimeOffset At, Func<Task> Callback)> Scheduled { get; } = [];
    public List<Guid> Cancelled { get; } = [];

    public void Schedule(Guid sessionId, DateTimeOffset at, Func<Task> callback) => Scheduled[sessionId] = (at, callback);

    public void Cancel(Guid sessionId)
    {
        Scheduled.Remove(sessionId);
        Cancelled.Add(sessionId);
    }

    public Task FireAsync(Guid sessionId) => Scheduled[sessionId].Callback();
}

internal static class JoinTokens
{
    public static IJoinTokenProtector Create(IDateTimeProvider clock) =>
        new JoinTokenProtector(new EphemeralDataProtectionProvider(), clock);
}

internal class FakeSessionMusicNotifier : ISessionMusicNotifier
{
    public List<(Guid SessionId, QueueItemDto Song)> SongAdded { get; } = [];
    public List<(Guid SessionId, int SongId)> SongRemoved { get; } = [];
    public List<(Guid SessionId, int? PreviousSongId, QueueItemDto? NowPlaying)> QueueAdvanced { get; } = [];
    public List<(Guid SessionId, PlayerStateChangedDto State)> PlayerStateChanged { get; } = [];
    public List<(Guid SessionId, PlayerCommandDto Command)> PlayerCommands { get; } = [];
    public List<(Guid SessionId, FallbackErrorDto Error)> FallbackErrors { get; } = [];
    public List<(Guid SessionId, LyricsOffsetChangedDto Offset)> LyricsOffsetChanges { get; } = [];
    public List<Guid> SlideshowChanges { get; } = [];
    public List<Guid> RingoChanges { get; } = [];

    public Task SongAddedAsync(Guid sessionId, QueueItemDto song, CancellationToken cancellationToken)
    {
        SongAdded.Add((sessionId, song));
        return Task.CompletedTask;
    }

    public Task SongRemovedAsync(Guid sessionId, int songId, CancellationToken cancellationToken)
    {
        SongRemoved.Add((sessionId, songId));
        return Task.CompletedTask;
    }

    public Task QueueAdvancedAsync(
        Guid sessionId,
        int? previousSongId,
        QueueItemDto? nowPlaying,
        CancellationToken cancellationToken)
    {
        QueueAdvanced.Add((sessionId, previousSongId, nowPlaying));
        return Task.CompletedTask;
    }

    public Task PlayerStateChangedAsync(
        Guid sessionId,
        PlayerStateChangedDto state,
        CancellationToken cancellationToken)
    {
        PlayerStateChanged.Add((sessionId, state));
        return Task.CompletedTask;
    }

    public Task PlayerCommandAsync(Guid sessionId, PlayerCommandDto command, CancellationToken cancellationToken)
    {
        PlayerCommands.Add((sessionId, command));
        return Task.CompletedTask;
    }

    public Task FallbackErrorChangedAsync(
        Guid sessionId,
        FallbackErrorDto error,
        CancellationToken cancellationToken)
    {
        FallbackErrors.Add((sessionId, error));
        return Task.CompletedTask;
    }

    public Task LyricsOffsetChangedAsync(
        Guid sessionId,
        LyricsOffsetChangedDto offset,
        CancellationToken cancellationToken)
    {
        LyricsOffsetChanges.Add((sessionId, offset));
        return Task.CompletedTask;
    }

    public Task SlideshowChangedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        SlideshowChanges.Add(sessionId);
        return Task.CompletedTask;
    }

    public Task RingoChangedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        RingoChanges.Add(sessionId);
        return Task.CompletedTask;
    }
}

internal class FakeFallbackQueueFiller : IFallbackQueueFiller
{
    public List<Guid> Ensured { get; } = [];

    public Task EnsureQueuedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        Ensured.Add(sessionId);
        return Task.CompletedTask;
    }
}
