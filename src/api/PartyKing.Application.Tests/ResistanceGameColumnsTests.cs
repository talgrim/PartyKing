using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PartyKing.Persistence.Database;

namespace PartyKing.Application.Tests;

/// <summary>
/// The columns of ResistanceGames that the module state objects (complex properties of the game) are stored in. They
/// are the stored schema: renaming one in the mapping without a migration would lose the data of the games in progress.
/// </summary>
public class ResistanceGameColumnsTests : IAsyncLifetime
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"partyking-columns-{Guid.NewGuid():N}.db");

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<WriterDbContext>().UseSqlite($"Data Source={_file}").Options;
        await using var dbContext = new WriterDbContext(options);
        await dbContext.Database.MigrateAsync();
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_file);

        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("InquisitorPlayerId", "TEXT", false)]
    [InlineData("InquisitionExaminerPlayerId", "TEXT", false)]
    [InlineData("DefectorDeck", "TEXT", true)]
    [InlineData("AssassinationTargetPlayerId", "TEXT", false)]
    public async Task TheModuleStateColumns_KeepTheirNamesTypesAndNullability(string column, string type, bool required)
    {
        await using var connection = new SqliteConnection($"Data Source={_file}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT type, \"notnull\" FROM pragma_table_info('ResistanceGames') WHERE name = $name";
        command.Parameters.AddWithValue("$name", column);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync(), $"ResistanceGames has no column {column}");
        Assert.Equal(type, reader.GetString(0));
        Assert.Equal(required, reader.GetInt32(1) == 1);
    }
}
