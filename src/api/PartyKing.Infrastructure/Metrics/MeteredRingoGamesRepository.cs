using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Infrastructure.Metrics;

internal sealed class MeteredRingoGamesRepository
    : MeteredLobbyGamesRepository<RingoGame, RingoPlayer>, IRingoGamesRepository
{
    private readonly IRingoGamesRepository _inner;

    public MeteredRingoGamesRepository(IRingoGamesRepository inner, RepositoryMetrics metrics)
        : base(nameof(RingoGamesRepository), inner, metrics)
    {
        _inner = inner;
    }

    public Task<RingoFlipResult> FlipAsync(
        Guid sessionId,
        int cardId,
        string flippedBy,
        Guid? turnOf,
        DateTimeOffset flippedAt,
        CancellationToken cancellationToken)
    {
        return MeasureAsync(
            nameof(FlipAsync),
            () => _inner.FlipAsync(sessionId, cardId, flippedBy, turnOf, flippedAt, cancellationToken));
    }

    public Task<RingoContinueResult> ContinueAsync(Guid sessionId, Guid? turnOf, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(ContinueAsync), () => _inner.ContinueAsync(sessionId, turnOf, cancellationToken));
    }
}
