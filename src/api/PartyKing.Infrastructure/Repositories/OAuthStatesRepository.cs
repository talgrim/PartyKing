using PartyKing.Application.Ports;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Auth;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

internal class OAuthStatesRepository : IOAuthStatesRepository
{
    private readonly IDbContextFactory<WriterDbContext> _writerDbContextFactory;

    public OAuthStatesRepository(IDbContextFactory<WriterDbContext> writerDbContextFactory)
    {
        _writerDbContextFactory = writerDbContextFactory;
    }

    public async Task<string> CreateAsync(Guid sessionId, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

        var now = DateTime.UtcNow;

        await dbContext.OAuthStates
            .Where(x => x.ExpiresAt < now)
            .ExecuteDeleteAsync(cancellationToken);

        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

        dbContext.OAuthStates.Add(new OAuthState(state, sessionId, now.Add(lifetime)));
        await dbContext.SaveChangesAsync(cancellationToken);

        return state;
    }

    public async Task<Guid?> ConsumeAsync(string state, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

        var record = await dbContext.OAuthStates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == state, cancellationToken);

        if (record is null)
        {
            return null;
        }

        // The delete decides who consumed it, so a replayed state can't be used twice
        var deleted = await dbContext.OAuthStates
            .Where(x => x.Id == state)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0 || record.ExpiresAt < DateTime.UtcNow)
        {
            return null;
        }

        return record.SessionId;
    }
}
