using PartyKing.Domain.Entities.Demo;

namespace PartyKing.Domain.Repositories;

public interface IDemoSessionsRepository
{
    /// <summary>The session must exist; the row goes away with it.</summary>
    Task AddAsync(DemoSession demoSession, CancellationToken cancellationToken);

    /// <summary>Null when the session is not a demo session.</summary>
    Task<DemoSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>How many demo sessions exist, expired ones that were not swept yet included.</summary>
    Task<int> CountAsync(CancellationToken cancellationToken);

    /// <summary>The demo sessions that are over at <paramref name="now"/> (UTC), the oldest first.</summary>
    Task<IReadOnlyList<DemoSession>> ListExpiredAsync(DateTime now, CancellationToken cancellationToken);
}
