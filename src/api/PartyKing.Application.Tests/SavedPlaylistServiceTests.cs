using ErrorOr;
using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Playlists.Services;
using PartyKing.Contract.V1.Enums;
using PartyKing.Domain.Entities.Playlists;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Enums;

namespace PartyKing.Application.Tests;

public class SavedPlaylistServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly Guid AccountId = Guid.NewGuid();

    private static readonly QueueSelection Selection = new(
    [
        new PlayedSongsSpecification(),
        new PlayingSongSpecification(),
        new WaitingSongsSpecification(),
        new FallbackSongsSpecification()
    ]);

    private readonly FakeQueueRepository _queue = new();
    private readonly FakeSavedPlaylistsRepository _playlists = new();
    private readonly FakeClock _clock = new();
    private readonly SavedPlaylistService _service;
    private readonly Guid _sessionId = Guid.NewGuid();

    public SavedPlaylistServiceTests()
    {
        _service = new SavedPlaylistService(
            _playlists,
            _queue,
            Selection,
            _clock,
            NullLogger<SavedPlaylistService>.Instance);
    }

    private void AddSong(int id, QueueItemStatus status, bool isFallback = false)
    {
        _queue.AllSongs.Add(
            new QueueItem(_sessionId, QueueItemSource.Spotify, $"spotify:track:{id}", $"Song {id}", "Artist", null, 1000, isFallback)
            {
                Id = id,
                Status = status
            });
    }

    private void AddASession()
    {
        AddSong(1, QueueItemStatus.Played);
        AddSong(2, QueueItemStatus.Playing);
        AddSong(3, QueueItemStatus.Queued);
        AddSong(4, QueueItemStatus.Queued, isFallback: true);
    }

    [Fact]
    public async Task Save_StoresTheChosenSections_ForTheAccount()
    {
        AddASession();

        var result = await _service.SaveQueueAsync(
            AccountId,
            _sessionId,
            " Friday ",
            [PlaylistSection.Current, PlaylistSection.Queue],
            Ct);

        Assert.False(result.IsError);
        var saved = Assert.Single(_playlists.Saved);
        Assert.Equal(AccountId, saved.OwnerAccountId);
        Assert.Equal("Friday", saved.Name);
        Assert.Equal(["spotify:track:2", "spotify:track:3"], saved.Items.Select(x => x.Url));
    }

    [Fact]
    public async Task Save_ReturnsTheNumberOfSongsThatWereSaved_AfterDeDuplication()
    {
        AddSong(1, QueueItemStatus.Played);
        _queue.AllSongs.Add(
            new QueueItem(_sessionId, QueueItemSource.Spotify, "spotify:track:1", "Again", null, null, null) { Id = 2 });

        var result = await _service.SaveQueueAsync(
            AccountId,
            _sessionId,
            "Friday",
            [PlaylistSection.History, PlaylistSection.Queue],
            Ct);

        Assert.Equal(1, result.Value.SongCount);
        Assert.Equal(saved().Id, result.Value.Id);
        Assert.Equal("Friday", result.Value.Name);

        SavedPlaylist saved() => Assert.Single(_playlists.Saved);
    }

    [Fact]
    public async Task Save_StampsThePlaylistWithTheTimeOfTheClock()
    {
        AddASession();

        await _service.SaveQueueAsync(AccountId, _sessionId, "Friday", [PlaylistSection.Queue], Ct);

        Assert.Equal(_clock.UtcNow.UtcDateTime, Assert.Single(_playlists.Saved).CreatedAt);
    }

    [Fact]
    public async Task Save_WithoutSections_IsRefused_AndStoresNothing()
    {
        AddASession();

        var result = await _service.SaveQueueAsync(AccountId, _sessionId, "Friday", [], Ct);

        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Empty(_playlists.Saved);
    }

    [Fact]
    public async Task Save_OfSectionsWithoutSongs_IsRefused()
    {
        AddSong(1, QueueItemStatus.Queued);

        var result = await _service.SaveQueueAsync(AccountId, _sessionId, "Friday", [PlaylistSection.History], Ct);

        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Empty(_playlists.Saved);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task Save_WithABlankName_IsRefused(string? name)
    {
        AddASession();

        var result = await _service.SaveQueueAsync(AccountId, _sessionId, name, [PlaylistSection.Queue], Ct);

        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("The name must not be blank", result.FirstError.Description);
        Assert.Empty(_playlists.Saved);
    }

    [Fact]
    public async Task Save_WithATooLongName_IsRefused()
    {
        AddASession();

        var result = await _service.SaveQueueAsync(
            AccountId,
            _sessionId,
            new string('a', SavedPlaylist.MaxNameLength + 1),
            [PlaylistSection.Queue],
            Ct);

        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal($"The name must be at most {SavedPlaylist.MaxNameLength} characters", result.FirstError.Description);
    }

    [Fact]
    public async Task Save_ByAnAccountAtItsLimit_IsRefused()
    {
        AddASession();
        _playlists.OwnerPlaylistCount = SavedPlaylist.MaxPerAccount;

        var result = await _service.SaveQueueAsync(AccountId, _sessionId, "Friday", [PlaylistSection.Queue], Ct);

        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal($"You can save at most {SavedPlaylist.MaxPerAccount} playlists", result.FirstError.Description);
        Assert.Empty(_playlists.Saved);
    }

    [Fact]
    public async Task Preview_CountsEverySection()
    {
        AddASession();

        var result = await _service.GetPreviewAsync(_sessionId, Ct);

        Assert.Equal(
            [
                (PlaylistSection.History, 1),
                (PlaylistSection.Current, 1),
                (PlaylistSection.Queue, 1),
                (PlaylistSection.Fallback, 1)
            ],
            result.Value.Sections.Select(x => (x.Section, x.Count)));
    }

    [Fact]
    public async Task Preview_OfAnEmptyQueue_CountsNothingInEverySection()
    {
        var result = await _service.GetPreviewAsync(_sessionId, Ct);

        Assert.All(result.Value.Sections, x => Assert.Equal(0, x.Count));
        Assert.Equal(4, result.Value.Sections.Count);
    }
}
