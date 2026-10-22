using ErrorOr;
using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities;
using PartyKing.Domain.Results;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

public interface ISpotifyProfilesRepository
{
    Task CreateProfileAsync(AuthenticationResult result, CancellationToken cancellationToken);
    Task<ErrorOr<SpotifyProfile>> GetProfileAsync(int id, CancellationToken cancellationToken);
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

        var profileId = int.Parse(result.ProfileId);

        if (await dbContext.SpotifyProfiles.AnyAsync(x => x.Id == profileId, cancellationToken))
        {
            return;
        }

        var profile = SpotifyProfile.Create(profileId, result.RefreshToken);

        await dbContext.SpotifyProfiles.AddAsync(profile, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ErrorOr<SpotifyProfile>> GetProfileAsync(int id, CancellationToken cancellationToken)
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