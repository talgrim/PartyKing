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

    public Task<RingoGame?> OpenAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(OpenAsync), () => _inner.OpenAsync(sessionId, cancellationToken));
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

    public Task<RingoPlayersResult> JoinAsync(Guid sessionId, RingoPlayer player, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(JoinAsync), () => _inner.JoinAsync(sessionId, player, cancellationToken));
    }

    public Task<RingoPlayersResult> KickAsync(Guid sessionId, Guid playerId, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(KickAsync), () => _inner.KickAsync(sessionId, playerId, cancellationToken));
    }

    public Task<RingoPlayersResult> ReorderAsync(
        Guid sessionId, IReadOnlyList<Guid> playerIds, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(ReorderAsync), () => _inner.ReorderAsync(sessionId, playerIds, cancellationToken));
    }

    public Task<bool> DeleteAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(DeleteAsync), () => _inner.DeleteAsync(sessionId, cancellationToken));
    }
}
