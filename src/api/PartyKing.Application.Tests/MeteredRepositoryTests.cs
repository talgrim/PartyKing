using PartyKing.Domain.Entities.Playlists;
using PartyKing.Domain.Repositories;
using System.Diagnostics.Metrics;
using PartyKing.Infrastructure.Metrics;

namespace PartyKing.Application.Tests;

public class MeteredRepositoryTests
{
    private sealed class FakeSavedPlaylistsRepository : ISavedPlaylistsRepository
    {
        public Exception? Failure { get; init; }

        public Task<int> CountByOwnerAsync(Guid ownerAccountId, CancellationToken cancellationToken) =>
            Run(() => 3);

        public Task<IReadOnlyList<SavedPlaylistSummary>> ListByOwnerAsync(
            Guid ownerAccountId,
            CancellationToken cancellationToken) =>
            Run<IReadOnlyList<SavedPlaylistSummary>>(() => []);

        public Task<SavedPlaylist?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Run<SavedPlaylist?>(() => null);

        public Task AddAsync(SavedPlaylist playlist, CancellationToken cancellationToken) =>
            Run(() => 0);

        public Task<int> DeleteByOwnerAsync(Guid ownerAccountId, CancellationToken cancellationToken) =>
            Run(() => 2);

        private Task<T> Run<T>(Func<T> result)
        {
            return Failure is null ? Task.FromResult(result()) : throw Failure;
        }
    }

    private static List<Dictionary<string, object?>> Capture(
        ISavedPlaylistsRepository inner,
        Func<ISavedPlaylistsRepository, Task> act)
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
            act(new MeteredSavedPlaylistsRepository(inner, metrics)).GetAwaiter().GetResult();
        }
        catch (InvalidOperationException)
        {
        }

        return tagSets;
    }

    [Fact]
    public void CountByOwnerAsync_OnSuccess_RecordsRepositoryOperationAndOutcome()
    {
        var tags = Assert.Single(Capture(new FakeSavedPlaylistsRepository(), r => r.CountByOwnerAsync(Guid.NewGuid(), default)));

        Assert.Equal("SavedPlaylistsRepository", tags["repository"]);
        Assert.Equal("CountByOwnerAsync", tags["operation"]);
        Assert.Equal(RepositoryMetrics.SuccessOutcome, tags["outcome"]);
    }

    [Fact]
    public void DeleteByOwnerAsync_RecordsTheCall()
    {
        var tags = Assert.Single(Capture(new FakeSavedPlaylistsRepository(), r => r.DeleteByOwnerAsync(Guid.NewGuid(), default)));

        Assert.Equal("DeleteByOwnerAsync", tags["operation"]);
        Assert.Equal(RepositoryMetrics.SuccessOutcome, tags["outcome"]);
    }

    [Fact]
    public void CountByOwnerAsync_WhenTheInnerCallThrows_RecordsAnExceptionOutcome()
    {
        var inner = new FakeSavedPlaylistsRepository { Failure = new InvalidOperationException("boom") };

        var tags = Assert.Single(Capture(inner, r => r.CountByOwnerAsync(Guid.NewGuid(), default)));

        Assert.Equal(RepositoryMetrics.ExceptionOutcome, tags["outcome"]);
    }

    [Fact]
    public async Task CountByOwnerAsync_ReturnsTheInnerResult()
    {
        using var metrics = new RepositoryMetrics();
        var repository = new MeteredSavedPlaylistsRepository(new FakeSavedPlaylistsRepository(), metrics);

        Assert.Equal(3, await repository.CountByOwnerAsync(Guid.NewGuid(), default));
    }
}
