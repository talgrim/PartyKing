using PartyKing.Domain.Repositories;
using ErrorOr;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using PartyKing.Application.Events;
using PartyKing.Application.Games.Services.Base;
using PartyKing.Application.Queue.Services;
using PartyKing.Application.Realtime;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.Slideshow.Services;
using PartyKing.Application.Spotify.Services;
using PartyKing.Application.System;
using PartyKing.Contract.V1.Lyrics;
using PartyKing.Contract.V1.Player;
using PartyKing.Contract.V1.Queue;
using PartyKing.Contract.V1.Slideshow;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Enums;
using PartyKing.Domain.Events;
using PartyKing.Domain.Events.Sessions;

namespace PartyKing.Application.Tests;

internal class FakeClock : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan by) => UtcNow += by;
}

internal class FakeSessionsRepository : ISessionsRepository
{
    private readonly List<Session> _sessions = [];

    /// <summary>Receives the events a change raised, like the real repository's dispatcher; none by default.</summary>
    public IDomainEventDispatcher? Events { get; set; }

    private Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events)
    {
        return Events is null || events.Count == 0
            ? Task.CompletedTask
            : Events.DispatchAsync(events, CancellationToken.None);
    }

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

    public async Task<bool> UpdatePasswordHashAsync(Guid id, string passwordHash, CancellationToken cancellationToken)
    {
        var session = _sessions.FirstOrDefault(x => x.Id == id);
        if (session is not null)
        {
            session.ChangePassword(passwordHash);
            await DispatchAsync(session.DrainEvents());
        }

        return session is not null;
    }

    public Task<IReadOnlyList<Session>> GetAllAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    public List<(Guid Id, string Name)> Renamed { get; } = [];

    public async Task<bool> RenameAsync(Guid id, string name, CancellationToken cancellationToken)
    {
        var session = _sessions.FirstOrDefault(x => x.Id == id);
        if (session is not null)
        {
            session.Rename(name);
            Renamed.Add((id, session.Name));
            await DispatchAsync(session.DrainEvents());
        }

        return session is not null;
    }
    public List<(Guid Id, string? PlaylistId, string? Name, bool Shuffle)> FallbackPlaylistSet { get; } = [];
    public List<(Guid Id, int Cursor)> FallbackCursorSet { get; } = [];

    public async Task<bool> SetFallbackPlaylistAsync(
        Guid id,
        string? playlistId,
        string? playlistName,
        bool shuffle,
        CancellationToken cancellationToken)
    {
        var session = _sessions.FirstOrDefault(x => x.Id == id);
        if (session is not null)
        {
            if (playlistId is null)
            {
                session.ClearFallbackPlaylist();
            }
            else
            {
                session.SetFallbackPlaylist(playlistId, playlistName, shuffle);
            }

            FallbackPlaylistSet.Add((id, playlistId, playlistName, shuffle));
            await DispatchAsync(session.DrainEvents());
        }

        return session is not null;
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

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var session = _sessions.FirstOrDefault(x => x.Id == id);
        if (session is null)
        {
            return false;
        }

        _sessions.Remove(session);
        await DispatchAsync([new SessionDeleted(id, session.HostProfileId, session.FallbackPlaylistId)]);

        return true;
    }
    public Task<SessionClaimResult> TryClaimAsync(Guid id, string profileId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public async Task<bool> ReleaseAsync(Guid id, CancellationToken cancellationToken)
    {
        var session = _sessions.FirstOrDefault(x => x.Id == id);
        if (session is not null)
        {
            session.Release();
            await DispatchAsync(session.DrainEvents());
        }

        return session is not null;
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

    /// <summary>False: the caller's view is outdated and the advance changes nothing.</summary>
    public bool AdvanceMovesOn { get; set; } = true;

    public Action? OnAdvance { get; set; }
    public List<(Guid SessionId, int ItemId)> RemoveCalls { get; } = [];
    public QueueItemRemoval RemoveResult { get; set; } = QueueItemRemoval.Removed;

    public Task<QueueItem?> GetPlayingAsync(Guid sessionId, CancellationToken cancellationToken) =>
        Task.FromResult(Playing);

    public Task<QueueAdvanceResult> AdvanceAsync(Guid sessionId, int? currentItemId, CancellationToken cancellationToken)
    {
        AdvanceCalls.Add((sessionId, currentItemId));
        OnAdvance?.Invoke();
        return Task.FromResult(new QueueAdvanceResult(
            AdvanceResult,
            AdvanceMovesOn,
            AdvanceMovesOn ? currentItemId : null));
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

internal class FakeQueueNotifier : IQueueNotifier
{
    public List<(Guid SessionId, QueueItemDto Song)> SongAdded { get; } = [];
    public List<(Guid SessionId, int SongId)> SongRemoved { get; } = [];
    public List<(Guid SessionId, int? PreviousSongId, QueueItemDto? NowPlaying)> QueueAdvanced { get; } = [];

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
}

internal class FakePlayerNotifier : IPlayerNotifier
{
    public List<(Guid SessionId, PlayerStateChangedDto State)> PlayerStateChanged { get; } = [];
    public List<(Guid SessionId, PlayerCommandDto Command)> PlayerCommands { get; } = [];
    public List<(Guid SessionId, FallbackErrorDto Error)> FallbackErrors { get; } = [];

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
}

internal class FakeLyricsNotifier : ILyricsNotifier
{
    public List<(Guid SessionId, LyricsOffsetChangedDto Offset)> LyricsOffsetChanges { get; } = [];

    public Task LyricsOffsetChangedAsync(
        Guid sessionId,
        LyricsOffsetChangedDto offset,
        CancellationToken cancellationToken)
    {
        LyricsOffsetChanges.Add((sessionId, offset));
        return Task.CompletedTask;
    }
}

internal class FakeSlideshowNotifier : ISlideshowNotifier
{
    public List<Guid> SlideshowChanges { get; } = [];

    public Task SlideshowChangedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        SlideshowChanges.Add(sessionId);
        return Task.CompletedTask;
    }
}

internal class FakeGameLobbyNotifier : IGameLobbyNotifier
{
    public List<Guid> Changes { get; } = [];
    public List<Guid> Opens { get; } = [];

    public Task OpenedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        Opens.Add(sessionId);
        return Task.CompletedTask;
    }

    public Task ChangedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        Changes.Add(sessionId);
        return Task.CompletedTask;
    }
}

internal class FakeRingoNotifier : FakeGameLobbyNotifier, IRingoNotifier;

internal class FakeResistanceNotifier : FakeGameLobbyNotifier, IResistanceNotifier;

internal class FakeQueueAdvancer : IQueueAdvancer
{
    public List<(Guid SessionId, int? CurrentSongId)> Calls { get; } = [];
    public ErrorOr<QueueItemDto> Result { get; set; } = Error.NotFound(description: "The queue is empty");

    public Task<ErrorOr<QueueItemDto>> AdvanceAsync(Guid sessionId, int? currentSongId, CancellationToken cancellationToken)
    {
        Calls.Add((sessionId, currentSongId));
        return Task.FromResult(Result);
    }
}

internal class FakeFallbackQueueFiller : IFallbackQueueFiller
{
    public List<Guid> Ensured { get; } = [];
    public Action? OnEnsure { get; set; }

    public Task EnsureQueuedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        Ensured.Add(sessionId);
        OnEnsure?.Invoke();
        return Task.CompletedTask;
    }
}

internal sealed class FakeHostSessionCleanup : IHostSessionCleanup
{
    public List<(Guid SessionId, string? HostProfileId)> Forgotten { get; } = [];

    public Task ForgetHostAsync(Guid sessionId, string? hostProfileId, CancellationToken cancellationToken)
    {
        Forgotten.Add((sessionId, hostProfileId));
        return Task.CompletedTask;
    }
}
