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

    public Session AddSession(string? hostProfileId = null)
    {
        var session = new Session("ABC123", "Party") { HostProfileId = hostProfileId };
        _sessions.Add(session);
        return session;
    }

    public Task<Session?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_sessions.FirstOrDefault(x => x.Id == id));

    public Task<bool> TryAddAsync(Session session, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IReadOnlyList<Session>> GetAllAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> RenameAsync(Guid id, string name, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<SessionClaimResult> TryClaimAsync(Guid id, string profileId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> ReleaseAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
}

internal class FakeQueueRepository : IQueueRepository
{
    public QueueItem? Playing { get; set; }
    public List<(Guid SessionId, int? CurrentItemId)> AdvanceCalls { get; } = [];
    public QueueItem? AdvanceResult { get; set; }

    public Task<QueueItem?> GetPlayingAsync(Guid sessionId, CancellationToken cancellationToken) =>
        Task.FromResult(Playing);

    public Task<QueueItem?> AdvanceAsync(Guid sessionId, int? currentItemId, CancellationToken cancellationToken)
    {
        AdvanceCalls.Add((sessionId, currentItemId));
        return Task.FromResult(AdvanceResult);
    }

    public Task<QueueItem> AddAsync(QueueItem item, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IReadOnlyList<QueueItem>> GetPendingAsync(Guid sessionId, CancellationToken cancellationToken) => throw new NotSupportedException();
}
