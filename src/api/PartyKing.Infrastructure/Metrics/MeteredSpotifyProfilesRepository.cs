using PartyKing.Application.Ports;
using ErrorOr;
using PartyKing.Domain.Entities.Spotify;
using PartyKing.Domain.Results;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Infrastructure.Metrics;

internal sealed class MeteredSpotifyProfilesRepository : MeteredRepository, ISpotifyProfilesRepository
{
    private readonly ISpotifyProfilesRepository _inner;

    public MeteredSpotifyProfilesRepository(ISpotifyProfilesRepository inner, RepositoryMetrics metrics)
        : base(nameof(SpotifyProfilesRepository), metrics)
    {
        _inner = inner;
    }

    public Task CreateProfileAsync(AuthenticationResult result, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(CreateProfileAsync), () => _inner.CreateProfileAsync(result, cancellationToken));
    }

    public Task<ErrorOr<SpotifyProfile>> GetProfileAsync(string id, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(GetProfileAsync), () => _inner.GetProfileAsync(id, cancellationToken));
    }
}
