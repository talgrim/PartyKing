using PartyKing.Domain.Repositories;
using System.Diagnostics.Metrics;
using PartyKing.Domain.Entities.Auth;
using PartyKing.Infrastructure.Metrics;

namespace PartyKing.Application.Tests;

public class MeteredRepositoryTests
{
    private sealed class FakeAccountsRepository : IAccountsRepository
    {
        public Exception? Failure { get; init; }

        public Task<bool> AnyAdminAsync(CancellationToken cancellationToken) =>
            Failure is null ? Task.FromResult(true) : throw Failure;

        public Task<Account?> GetByUsernameAsync(string username, CancellationToken cancellationToken) =>
            Task.FromResult<Account?>(null);

        public Task<Account?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Account?>(null);

        public Task<bool> TryAddAsync(Account account, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<bool> ChangePasswordAsync(Guid id, string passwordHash, Guid newSecurityStamp, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> SignOutEverywhereAsync(Guid id, Guid newSecurityStamp, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<AccountPage> GetUsersPageAsync(string? search, int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult(new AccountPage([], 0));
    }

    private static List<Dictionary<string, object?>> Capture(
        IAccountsRepository inner,
        Func<IAccountsRepository, Task> act)
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
            act(new MeteredAccountsRepository(inner, metrics)).GetAwaiter().GetResult();
        }
        catch (InvalidOperationException)
        {
        }

        return tagSets;
    }

    [Fact]
    public void AnyAdminAsync_OnSuccess_RecordsRepositoryOperationAndOutcome()
    {
        var tags = Assert.Single(Capture(new FakeAccountsRepository(), r => r.AnyAdminAsync(default)));

        Assert.Equal("AccountsRepository", tags["repository"]);
        Assert.Equal("AnyAdminAsync", tags["operation"]);
        Assert.Equal(RepositoryMetrics.SuccessOutcome, tags["outcome"]);
    }

    [Fact]
    public void TryAddAsync_RecordsTheCall()
    {
        var tags = Assert.Single(Capture(new FakeAccountsRepository(), r => r.TryAddAsync(Account.Create(Guid.NewGuid(), "admin", "hash", AccountRole.Admin, Guid.NewGuid(), DateTime.UtcNow).Account!, default)));

        Assert.Equal("TryAddAsync", tags["operation"]);
        Assert.Equal(RepositoryMetrics.SuccessOutcome, tags["outcome"]);
    }

    [Fact]
    public void AnyAdminAsync_WhenTheInnerCallThrows_RecordsAnExceptionOutcome()
    {
        var inner = new FakeAccountsRepository { Failure = new InvalidOperationException("boom") };

        var tags = Assert.Single(Capture(inner, r => r.AnyAdminAsync(default)));

        Assert.Equal(RepositoryMetrics.ExceptionOutcome, tags["outcome"]);
    }

    [Fact]
    public async Task AnyAdminAsync_ReturnsTheInnerResult()
    {
        using var metrics = new RepositoryMetrics();
        var repository = new MeteredAccountsRepository(new FakeAccountsRepository(), metrics);

        Assert.True(await repository.AnyAdminAsync(default));
    }
}
