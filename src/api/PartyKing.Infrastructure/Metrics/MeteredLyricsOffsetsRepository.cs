using PartyKing.Domain.Entities.Lyrics;
using PartyKing.Domain.Repositories;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Infrastructure.Metrics;

internal sealed class MeteredLyricsOffsetsRepository : MeteredRepository, ILyricsOffsetsRepository
{
    private readonly ILyricsOffsetsRepository _inner;

    public MeteredLyricsOffsetsRepository(ILyricsOffsetsRepository inner, RepositoryMetrics metrics)
        : base(nameof(LyricsOffsetsRepository), metrics)
    {
        _inner = inner;
    }

    public Task<int> GetAsync(string songUrl, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(GetAsync), () => _inner.GetAsync(songUrl, cancellationToken));
    }

    public Task<LyricsOffsetFailure> ChangeAsync(
        string songUrl,
        int offsetMs,
        Guid sessionId,
        int songId,
        CancellationToken cancellationToken)
    {
        return MeasureAsync(
            nameof(ChangeAsync),
            () => _inner.ChangeAsync(songUrl, offsetMs, sessionId, songId, cancellationToken));
    }
}
