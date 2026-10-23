using ErrorOr;
using Microsoft.AspNetCore.Http;
using PartyKing.Application.Slideshow.Services;
using PartyKing.Contract.V1.Slideshow;
using PartyKing.Application.Spotify.Services;
using PartyKing.Application.System;
using PartyKing.Domain.Entities;
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
    public Task<bool> RenameAsync(Guid id, string name, CancellationToken cancellationToken) => throw new NotSupportedException();
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

    public Task<ErrorOr<string>> GetSessionAccessTokenAsync(Guid sessionId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

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

    public Task<QueueItem> AddAsync(QueueItem item, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IReadOnlyList<QueueItem>> GetPendingAsync(Guid sessionId, CancellationToken cancellationToken) => throw new NotSupportedException();
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
}
