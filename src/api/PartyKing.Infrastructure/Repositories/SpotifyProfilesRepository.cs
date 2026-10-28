using ErrorOr;
using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Spotify;
using PartyKing.Domain.Results;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

public interface ISpotifyProfilesRepository
{
    Task CreateProfileAsync(AuthenticationResult result, CancellationToken cancellationToken);
    Task<ErrorOr<SpotifyProfile>> GetProfileAsync(string id, CancellationToken cancellationToken);
}

internal class SpotifyProfilesRepository : ISpotifyProfilesRepository
{
    private readonly IDbContextFactory<ReaderDbContext> _readerDbContextFactory;
    private readonly IDbContextFactory<WriterDbContext> _writerDbContextFactory;

    public SpotifyProfilesRepository(
        IDbContextFactory<ReaderDbContext> readerDbContextFactory,
        IDbContextFactory<WriterDbContext> writerDbContextFactory)
    {
        _readerDbContextFactory = readerDbContextFactory;
        _writerDbContextFactory = writerDbContextFactory;
    }

    public async Task CreateProfileAsync(AuthenticationResult result, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

        var profileId = result.ProfileId;

        var existing = await dbContext.SpotifyProfiles.FirstOrDefaultAsync(x => x.Id == profileId, cancellationToken);

        if (existing is not null)
        {
            // Re-login: replace the stored token, the old one may have been revoked or expired
            existing.UpdateRefreshToken(result.RefreshToken);
            existing.UpdateDisplayName(result.DisplayName);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var profile = SpotifyProfile.Create(profileId, result.RefreshToken);
        profile.UpdateDisplayName(result.DisplayName);

        await dbContext.SpotifyProfiles.AddAsync(profile, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ErrorOr<SpotifyProfile>> GetProfileAsync(string id, CancellationToken cancellationToken)
    {
        await using var dbContext = await _readerDbContextFactory.CreateDbContextAsync(cancellationToken);

        var profile = await dbContext.SpotifyProfiles.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (profile is null)
        {
            return Error.NotFound(description: $"Profile with ID {id} was not found in the database");
        }

        return profile;
    }
}
