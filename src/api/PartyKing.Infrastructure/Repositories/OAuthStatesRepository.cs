using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Auth;
using PartyKing.Domain.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

internal class OAuthStatesRepository : IOAuthStatesRepository
{
    private readonly IDbContextFactory<WriterDbContext> _writerDbContextFactory;

    public OAuthStatesRepository(IDbContextFactory<WriterDbContext> writerDbContextFactory)
    {
        _writerDbContextFactory = writerDbContextFactory;
    }

    public async Task AddAsync(OAuthState state, DateTime now, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

        await dbContext.OAuthStates
            .Where(x => x.ExpiresAt < now)
            .ExecuteDeleteAsync(cancellationToken);

        dbContext.OAuthStates.Add(state);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<OAuthState?> TakeAsync(string id, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

        var state = await dbContext.OAuthStates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (state is null)
        {
            return null;
        }

        // The delete decides who took it, so a replayed state can't be used twice
        var deleted = await dbContext.OAuthStates
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 0 ? null : state;
    }
}
