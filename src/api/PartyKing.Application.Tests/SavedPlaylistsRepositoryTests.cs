using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Domain.Entities.Playlists;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Enums;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Application.Tests;

/// <summary>The saved playlists repository against a real SQLite file: the copy of the songs, their order, and deleting an owner's playlists.</summary>
public class SavedPlaylistsRepositoryTests : IAsyncLifetime
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"partyking-tests-{Guid.NewGuid():N}");
    private string ConnectionString => $"Data Source={Path.Combine(_directory, "PartyKing.db")}";
    private ServiceProvider _provider = null!;
    private IDbContextFactory<WriterDbContext> _writer = null!;
    private SavedPlaylistsRepository _repository = null!;
    private readonly Guid _owner = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);

        _provider = new ServiceCollection()
            .AddDbContextFactory<WriterDbContext>(options => options.UseSqlite(ConnectionString))
            .AddDbContextFactory<ReaderDbContext>(options => options.UseSqlite(ConnectionString))
            .BuildServiceProvider();

        _writer = _provider.GetRequiredService<IDbContextFactory<WriterDbContext>>();
        await using (var dbContext = await _writer.CreateDbContextAsync())
        {
            await dbContext.Database.MigrateAsync();
        }

        _repository = new SavedPlaylistsRepository(
            _provider.GetRequiredService<IDbContextFactory<ReaderDbContext>>(),
            _writer);
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    private static SavedPlaylist NewPlaylist(Guid ownerId, string name = "Friday", int songs = 3, DateTime? createdAt = null)
    {
        var queue = Enumerable.Range(1, songs)
            .Select(x => new QueueItem(
                Guid.NewGuid(),
                x % 2 == 0 ? QueueItemSource.Youtube : QueueItemSource.Spotify,
                $"url-{x}",
                $"Song {x}",
                x == 1 ? "Artist" : null,
                x == 1 ? "https://art/1" : null,
                x == 1 ? 1000 : null)
            {
                Id = x
            })
            .ToList();

        return SavedPlaylist.Create(Guid.NewGuid(), ownerId, name, queue, 0, createdAt ?? Now).Playlist!;
    }

    private async Task<SavedPlaylist?> ReadAsync(Guid id)
    {
        await using var dbContext = await _writer.CreateDbContextAsync();

        return await dbContext.SavedPlaylists.Include(x => x.Items).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    }

    [Fact]
    public async Task AnAddedPlaylist_IsReadBackWithItsSongsInOrder()
    {
        var playlist = NewPlaylist(_owner);

        await _repository.AddAsync(playlist, Ct);

        var read = await ReadAsync(playlist.Id);
        Assert.Equal("Friday", read!.Name);
        Assert.Equal(_owner, read.OwnerAccountId);
        Assert.Equal(Now, read.CreatedAt);
        Assert.Equal(["url-1", "url-2", "url-3"], read.Items.OrderBy(x => x.Position).Select(x => x.Url));
    }

    [Fact]
    public async Task ASong_IsStoredWithAllItsFields()
    {
        var playlist = NewPlaylist(_owner);

        await _repository.AddAsync(playlist, Ct);

        var first = (await ReadAsync(playlist.Id))!.Items.Single(x => x.Position == 0);
        Assert.Equal(QueueItemSource.Spotify, first.Source);
        Assert.Equal("Song 1", first.Title);
        Assert.Equal("Artist", first.Artists);
        Assert.Equal("https://art/1", first.AlbumArtUrl);
        Assert.Equal(1000, first.DurationMs);
        Assert.Equal(QueueItemSource.Youtube, (await ReadAsync(playlist.Id))!.Items.Single(x => x.Position == 1).Source);
    }

    [Fact]
    public async Task CountByOwner_CountsOnlyThePlaylistsOfThatAccount()
    {
        var other = Guid.NewGuid();
        await _repository.AddAsync(NewPlaylist(_owner, "One"), Ct);
        await _repository.AddAsync(NewPlaylist(_owner, "Two"), Ct);
        await _repository.AddAsync(NewPlaylist(other, "Three"), Ct);

        Assert.Equal(2, await _repository.CountByOwnerAsync(_owner, Ct));
        Assert.Equal(1, await _repository.CountByOwnerAsync(other, Ct));
        Assert.Equal(0, await _repository.CountByOwnerAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task ListByOwner_ReturnsOnlyThePlaylistsOfThatAccount_NewestFirst_WithTheirSongCount()
    {
        var other = Guid.NewGuid();
        await _repository.AddAsync(NewPlaylist(_owner, "Old", songs: 1, createdAt: Now.AddDays(-2)), Ct);
        await _repository.AddAsync(NewPlaylist(_owner, "New", songs: 4, createdAt: Now), Ct);
        await _repository.AddAsync(NewPlaylist(other, "Theirs"), Ct);

        var list = await _repository.ListByOwnerAsync(_owner, Ct);

        Assert.Equal([("New", 4), ("Old", 1)], list.Select(x => (x.Name, x.SongCount)));
        Assert.Equal(Now, list[0].CreatedAt);
    }

    [Fact]
    public async Task ListByOwner_OfAnAccountWithoutPlaylists_IsEmpty()
    {
        await _repository.AddAsync(NewPlaylist(_owner), Ct);

        Assert.Empty(await _repository.ListByOwnerAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task Get_ReturnsThePlaylistWithItsSongsInTheSavedOrder()
    {
        var playlist = NewPlaylist(_owner, songs: 5);
        await _repository.AddAsync(playlist, Ct);

        var read = await _repository.GetAsync(playlist.Id, Ct);

        Assert.Equal(_owner, read!.OwnerAccountId);
        Assert.Equal(["url-1", "url-2", "url-3", "url-4", "url-5"], read.Items.Select(x => x.Url));
        Assert.Equal(
            [QueueItemSource.Spotify, QueueItemSource.Youtube, QueueItemSource.Spotify, QueueItemSource.Youtube, QueueItemSource.Spotify],
            read.Items.Select(x => x.Source));
    }

    [Fact]
    public async Task Get_OfAnUnknownPlaylist_IsNull()
    {
        Assert.Null(await _repository.GetAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task TwoPlaylistsOfTheSameAccount_MayShareAName()
    {
        await _repository.AddAsync(NewPlaylist(_owner, "Same"), Ct);
        await _repository.AddAsync(NewPlaylist(_owner, "Same"), Ct);

        Assert.Equal(2, await _repository.CountByOwnerAsync(_owner, Ct));
    }

    [Fact]
    public async Task DeleteByOwner_DeletesThePlaylistsOfThatOwnerAndTheirSongs_AndNoOthers()
    {
        var other = Guid.NewGuid();
        var mine = NewPlaylist(_owner);
        var theirs = NewPlaylist(other);
        await _repository.AddAsync(mine, Ct);
        await _repository.AddAsync(NewPlaylist(_owner, "Second"), Ct);
        await _repository.AddAsync(theirs, Ct);

        var deleted = await _repository.DeleteByOwnerAsync(_owner, Ct);

        Assert.Equal(2, deleted);
        Assert.Null(await ReadAsync(mine.Id));
        Assert.NotNull(await ReadAsync(theirs.Id));
        await using var check = await _writer.CreateDbContextAsync();
        Assert.Equal(theirs.Items.Count, await check.Set<SavedPlaylistItem>().CountAsync());
    }

    [Fact]
    public async Task DeleteByOwner_OfAnOwnerWithoutPlaylists_DeletesNothing()
    {
        Assert.Equal(0, await _repository.DeleteByOwnerAsync(Guid.NewGuid(), Ct));
    }
}
