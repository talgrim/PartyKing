using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Domain.Entities.Auth;
using PartyKing.Domain.Entities.Playlists;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Enums;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Application.Tests;

/// <summary>The saved playlists repository against a real SQLite file: the copy of the songs, their order, and the cascade from the account.</summary>
public class SavedPlaylistsRepositoryTests : IAsyncLifetime
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"partyking-tests-{Guid.NewGuid():N}");
    private string ConnectionString => $"Data Source={Path.Combine(_directory, "PartyKing.db")}";
    private ServiceProvider _provider = null!;
    private IDbContextFactory<WriterDbContext> _writer = null!;
    private SavedPlaylistsRepository _repository = null!;
    private Account _owner = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);

        _provider = new ServiceCollection()
            .AddDbContextFactory<WriterDbContext>(options => options.UseSqlite(ConnectionString))
            .AddDbContextFactory<ReaderDbContext>(options => options.UseSqlite(ConnectionString))
            .BuildServiceProvider();

        _writer = _provider.GetRequiredService<IDbContextFactory<WriterDbContext>>();
        _owner = await AddAccountAsync("ann");

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

    private async Task<Account> AddAccountAsync(string username)
    {
        await using var dbContext = await _writer.CreateDbContextAsync();
        await dbContext.Database.MigrateAsync();

        var account = Account.Create(Guid.NewGuid(), username, "hash", AccountRole.User, Guid.NewGuid(), Now).Account!;
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();

        return account;
    }

    private static SavedPlaylist NewPlaylist(Guid ownerId, string name = "Friday", int songs = 3)
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

        return SavedPlaylist.Create(Guid.NewGuid(), ownerId, name, queue, 0, Now).Playlist!;
    }

    private async Task<SavedPlaylist?> ReadAsync(Guid id)
    {
        await using var dbContext = await _writer.CreateDbContextAsync();

        return await dbContext.SavedPlaylists.Include(x => x.Items).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    }

    [Fact]
    public async Task AnAddedPlaylist_IsReadBackWithItsSongsInOrder()
    {
        var playlist = NewPlaylist(_owner.Id);

        await _repository.AddAsync(playlist, Ct);

        var read = await ReadAsync(playlist.Id);
        Assert.Equal("Friday", read!.Name);
        Assert.Equal(_owner.Id, read.OwnerAccountId);
        Assert.Equal(Now, read.CreatedAt);
        Assert.Equal(["url-1", "url-2", "url-3"], read.Items.OrderBy(x => x.Position).Select(x => x.Url));
    }

    [Fact]
    public async Task ASong_IsStoredWithAllItsFields()
    {
        var playlist = NewPlaylist(_owner.Id);

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
        var other = await AddAccountAsync("bob");
        await _repository.AddAsync(NewPlaylist(_owner.Id, "One"), Ct);
        await _repository.AddAsync(NewPlaylist(_owner.Id, "Two"), Ct);
        await _repository.AddAsync(NewPlaylist(other.Id, "Three"), Ct);

        Assert.Equal(2, await _repository.CountByOwnerAsync(_owner.Id, Ct));
        Assert.Equal(1, await _repository.CountByOwnerAsync(other.Id, Ct));
        Assert.Equal(0, await _repository.CountByOwnerAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task TwoPlaylistsOfTheSameAccount_MayShareAName()
    {
        await _repository.AddAsync(NewPlaylist(_owner.Id, "Same"), Ct);
        await _repository.AddAsync(NewPlaylist(_owner.Id, "Same"), Ct);

        Assert.Equal(2, await _repository.CountByOwnerAsync(_owner.Id, Ct));
    }

    [Fact]
    public async Task DeletingTheAccount_DeletesItsPlaylistsAndTheirSongs()
    {
        var playlist = NewPlaylist(_owner.Id);
        await _repository.AddAsync(playlist, Ct);

        await using (var dbContext = await _writer.CreateDbContextAsync())
        {
            await dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");
            await dbContext.Accounts.Where(x => x.Id == _owner.Id).ExecuteDeleteAsync();
        }

        Assert.Null(await ReadAsync(playlist.Id));
        await using var check = await _writer.CreateDbContextAsync();
        Assert.Equal(0, await check.Set<SavedPlaylistItem>().CountAsync());
    }

    [Fact]
    public async Task APlaylistOfAnUnknownAccount_IsNotStored()
    {
        await Assert.ThrowsAsync<DbUpdateException>(() => _repository.AddAsync(NewPlaylist(Guid.NewGuid()), Ct));
    }
}
