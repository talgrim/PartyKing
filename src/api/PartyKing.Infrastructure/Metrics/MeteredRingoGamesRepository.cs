using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Infrastructure.Metrics;

internal sealed class MeteredRingoGamesRepository : MeteredRepository, IRingoGamesRepository
{
    private readonly IRingoGamesRepository _inner;

    public MeteredRingoGamesRepository(IRingoGamesRepository inner, RepositoryMetrics metrics)
        : base(nameof(RingoGamesRepository), metrics)
    {
        _inner = inner;
    }

    public Task<RingoGame?> GetAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(GetAsync), () => _inner.GetAsync(sessionId, cancellationToken));
    }

    public Task<bool> TryCreateAsync(RingoGame game, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(TryCreateAsync), () => _inner.TryCreateAsync(game, cancellationToken));
    }

    public Task<RingoFlipResult> FlipAsync(
        Guid sessionId, int cardId, string flippedBy, DateTimeOffset flippedAt, CancellationToken cancellationToken)
    {
        return MeasureAsync(
            nameof(FlipAsync),
            () => _inner.FlipAsync(sessionId, cardId, flippedBy, flippedAt, cancellationToken));
    }

    public Task<RingoGame?> ContinueAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(ContinueAsync), () => _inner.ContinueAsync(sessionId, cancellationToken));
    }

    public Task<bool> DeleteAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(DeleteAsync), () => _inner.DeleteAsync(sessionId, cancellationToken));
    }
}
