using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PartyKing.Persistence.Database;

namespace PartyKing.API;

/// <summary>
/// Reaches the database through a new context, so /health fails when requests that need the DB hang
/// (the trivial "self" check alone passes while the rest of the API is stuck).
/// </summary>
public sealed class DatabaseHealthCheck(IDbContextFactory<ReaderDbContext> contextFactory) : IHealthCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync(timeout.Token);
            return await db.Database.CanConnectAsync(timeout.Token)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Cannot connect to the database");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy($"Database did not answer within {Timeout.TotalSeconds:0} s");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database check failed", ex);
        }
    }
}
