using PartyKing.Application.Ports;
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

    public Task SaveAsync(string songUrl, int offsetMs, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(SaveAsync), () => _inner.SaveAsync(songUrl, offsetMs, cancellationToken));
    }
}
