using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PartyKing.Domain.Enums;
using PartyKing.Persistence.Database;

namespace PartyKing.Application.Tests;

/// <summary>The migration that told Spotify playlists from saved ones, run over a database that has the old schema and sessions with and without a playlist.</summary>
public class FallbackPlaylistKindMigrationTests : IAsyncLifetime
{
    private const string MigrationBefore = "20261109124015_AddedSavedPlaylists";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"partyking-tests-{Guid.NewGuid():N}");

    private DbContextOptions<WriterDbContext> Options =>
        new DbContextOptionsBuilder<WriterDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_directory, "PartyKing.db")}")
            .Options;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);

        return Task.CompletedTask;
    }

    [Fact]
    public async Task APlaylistSetBeforeTheKindExisted_IsASpotifyPlaylistAfterwards_AndNoPlaylistHasNoKind()
    {
        var withPlaylist = Guid.NewGuid();
        var withoutPlaylist = Guid.NewGuid();

        await using (var before = new WriterDbContext(Options))
        {
            await before.GetService<IMigrator>().MigrateAsync(MigrationBefore);
            await before.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "Sessions" ("Id", "Code", "Name", "PasswordHash", "CreatedAt", "FallbackPlaylistId",
                    "FallbackPlaylistName", "FallbackShuffle", "FallbackCursor")
                VALUES ({0}, 'ABC123', 'Party', 'hash', '2026-11-09 12:00:00', 'spotify-playlist', 'Mix', 1, 3),
                       ({1}, 'DEF456', 'Other', 'hash', '2026-11-09 12:00:00', NULL, NULL, 0, 0)
                """,
                withPlaylist,
                withoutPlaylist);
        }

        await using var after = new WriterDbContext(Options);
        await after.Database.MigrateAsync();

        var sessions = await after.Sessions.AsNoTracking().ToDictionaryAsync(x => x.Id);
        Assert.Equal(
            ("spotify-playlist", FallbackPlaylistKind.Spotify, (Guid?)null, true, 3),
            (sessions[withPlaylist].FallbackPlaylistId, sessions[withPlaylist].FallbackPlaylistKind, sessions[withPlaylist].FallbackSavedPlaylistId, sessions[withPlaylist].FallbackShuffle, sessions[withPlaylist].FallbackCursor));
        Assert.Equal((null, null), (sessions[withoutPlaylist].FallbackPlaylistId, sessions[withoutPlaylist].FallbackPlaylistKind));
    }
}
