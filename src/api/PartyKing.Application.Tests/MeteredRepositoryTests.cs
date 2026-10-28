using System.Diagnostics.Metrics;
using PartyKing.Domain.Entities.Auth;
using PartyKing.Infrastructure.Metrics;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Application.Tests;

public class MeteredRepositoryTests
{
    private sealed class FakeAdminAccountsRepository : IAdminAccountsRepository
    {
        public Exception? Failure { get; init; }

        public Task<bool> AnyAsync(CancellationToken cancellationToken) =>
            Failure is null ? Task.FromResult(true) : throw Failure;

        public Task<AdminAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken) =>
            Task.FromResult<AdminAccount?>(null);

        public Task<AdminAccount?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<AdminAccount?>(null);

        public Task AddAsync(AdminAccount account, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static List<Dictionary<string, object?>> Capture(
        IAdminAccountsRepository inner,
        Func<IAdminAccountsRepository, Task> act)
    {
        var tagSets = new List<Dictionary<string, object?>>();
        using var metrics = new RepositoryMetrics();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == RepositoryMetrics.DurationInstrumentName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((_, _, tags, _) =>
            tagSets.Add(tags.ToArray().ToDictionary(t => t.Key, t => t.Value)));
        listener.Start();

        try
        {
            act(new MeteredAdminAccountsRepository(inner, metrics)).GetAwaiter().GetResult();
        }
        catch (InvalidOperationException)
        {
        }

        return tagSets;
    }

    [Fact]
    public void AnyAsync_OnSuccess_RecordsRepositoryOperationAndOutcome()
    {
        var tags = Assert.Single(Capture(new FakeAdminAccountsRepository(), r => r.AnyAsync(default)));

        Assert.Equal("AdminAccountsRepository", tags["repository"]);
        Assert.Equal("AnyAsync", tags["operation"]);
        Assert.Equal(RepositoryMetrics.SuccessOutcome, tags["outcome"]);
    }

    [Fact]
    public void AddAsync_WithoutAResult_RecordsTheCall()
    {
        var tags = Assert.Single(Capture(new FakeAdminAccountsRepository(), r => r.AddAsync(new AdminAccount("admin", "hash"), default)));

        Assert.Equal("AddAsync", tags["operation"]);
        Assert.Equal(RepositoryMetrics.SuccessOutcome, tags["outcome"]);
    }

    [Fact]
    public void AnyAsync_WhenTheInnerCallThrows_RecordsAnExceptionOutcome()
    {
        var inner = new FakeAdminAccountsRepository { Failure = new InvalidOperationException("boom") };

        var tags = Assert.Single(Capture(inner, r => r.AnyAsync(default)));

        Assert.Equal(RepositoryMetrics.ExceptionOutcome, tags["outcome"]);
    }

    [Fact]
    public async Task AnyAsync_ReturnsTheInnerResult()
    {
        using var metrics = new RepositoryMetrics();
        var repository = new MeteredAdminAccountsRepository(new FakeAdminAccountsRepository(), metrics);

        Assert.True(await repository.AnyAsync(default));
    }
}
