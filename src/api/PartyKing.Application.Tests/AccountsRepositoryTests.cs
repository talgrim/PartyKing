using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Domain.Entities.Auth;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Application.Tests;

/// <summary>The accounts repository against a real SQLite file: the unique, case-insensitive username, the users page and its search, and the updates.</summary>
public class AccountsRepositoryTests : IAsyncLifetime
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"partyking-tests-{Guid.NewGuid():N}");
    private string ConnectionString => $"Data Source={Path.Combine(_directory, "PartyKing.db")}";
    private ServiceProvider _provider = null!;
    private AccountsRepository _repository = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);

        _provider = new ServiceCollection()
            .AddDbContextFactory<WriterDbContext>(options => options.UseSqlite(ConnectionString))
            .AddDbContextFactory<ReaderDbContext>(options => options.UseSqlite(ConnectionString))
            .BuildServiceProvider();

        await using (var dbContext = await _provider.GetRequiredService<IDbContextFactory<WriterDbContext>>().CreateDbContextAsync())
        {
            await dbContext.Database.MigrateAsync();
        }

        _repository = new AccountsRepository(
            _provider.GetRequiredService<IDbContextFactory<ReaderDbContext>>(),
            _provider.GetRequiredService<IDbContextFactory<WriterDbContext>>());
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    private static Account NewAccount(string username, AccountRole role = AccountRole.User)
    {
        return Account.Create(Guid.NewGuid(), username, "hash", role, Guid.NewGuid(), Now).Account!;
    }

    private async Task<Account> AddAsync(string username, AccountRole role = AccountRole.User)
    {
        var account = NewAccount(username, role);
        Assert.True(await _repository.TryAddAsync(account, Ct));

        return account;
    }

    [Fact]
    public async Task AnAddedAccount_IsReadBackWithEverything()
    {
        var account = await AddAsync("Ann", AccountRole.Admin);

        var read = await _repository.GetAsync(account.Id, Ct);

        Assert.Equal("Ann", read!.Username);
        Assert.Equal("hash", read.PasswordHash);
        Assert.Equal(AccountRole.Admin, read.Role);
        Assert.Equal(account.SecurityStamp, read.SecurityStamp);
        Assert.Equal(Now, read.CreatedAt);
    }

    [Theory]
    [InlineData("ann")]
    [InlineData("ANN")]
    [InlineData("aNn")]
    public async Task ATakenUsername_CannotBeAddedAgain_WhateverTheCase(string username)
    {
        await AddAsync("Ann");

        var added = await _repository.TryAddAsync(NewAccount(username), Ct);

        Assert.False(added);
    }

    [Fact]
    public async Task AUsername_IsFoundWhateverTheCase()
    {
        var account = await AddAsync("Ann");

        var found = await _repository.GetByUsernameAsync("aNN", Ct);

        Assert.Equal(account.Id, found!.Id);
    }

    [Fact]
    public async Task AnyAdmin_IsTrueOnlyWhenAnAdminExists()
    {
        await AddAsync("ann");
        Assert.False(await _repository.AnyAdminAsync(Ct));

        await AddAsync("boss", AccountRole.Admin);

        Assert.True(await _repository.AnyAdminAsync(Ct));
    }

    [Fact]
    public async Task TheUsersPage_HoldsOnlyUsers_OrderedByUsername_WithTheTotal()
    {
        await AddAsync("cara");
        await AddAsync("Abel");
        await AddAsync("boss", AccountRole.Admin);
        await AddAsync("bob");

        var page = await _repository.GetUsersPageAsync(null, 1, 10, Ct);

        Assert.Equal(["Abel", "bob", "cara"], page.Items.Select(x => x.Username));
        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public async Task TheUsersPage_SkipsThePagesBefore_AndKeepsTheTotalOfAll()
    {
        for (var i = 0; i < 7; i++)
        {
            await AddAsync($"user{i}");
        }

        var second = await _repository.GetUsersPageAsync(null, 2, 3, Ct);
        var third = await _repository.GetUsersPageAsync(null, 3, 3, Ct);
        var beyond = await _repository.GetUsersPageAsync(null, 4, 3, Ct);

        Assert.Equal(["user3", "user4", "user5"], second.Items.Select(x => x.Username));
        Assert.Equal(["user6"], third.Items.Select(x => x.Username));
        Assert.Empty(beyond.Items);
        Assert.Equal(7, beyond.TotalCount);
    }

    [Fact]
    public async Task TheSearch_KeepsTheUsernamesThatContainIt_WhateverTheCase_AndCountsOnlyThose()
    {
        await AddAsync("Anna");
        await AddAsync("Hannah");
        await AddAsync("bob");

        var page = await _repository.GetUsersPageAsync("  ANN ", 1, 10, Ct);

        Assert.Equal(["Anna", "Hannah"], page.Items.Select(x => x.Username));
        Assert.Equal(2, page.TotalCount);
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("\\")]
    public async Task TheSearch_TakesWildcardCharactersLiterally(string search)
    {
        await AddAsync("plain");
        var special = await AddAsync($"a{search}b");

        var page = await _repository.GetUsersPageAsync(search, 1, 10, Ct);

        Assert.Equal(special.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task ChangingThePassword_StoresTheHashAndTheStamp()
    {
        var account = await AddAsync("ann");
        var stamp = Guid.NewGuid();

        var changed = await _repository.ChangePasswordAsync(account.Id, "new-hash", stamp, Ct);

        var read = await _repository.GetAsync(account.Id, Ct);
        Assert.True(changed);
        Assert.Equal("new-hash", read!.PasswordHash);
        Assert.Equal(stamp, read.SecurityStamp);
    }

    [Fact]
    public async Task SigningOutEverywhere_StoresTheStamp_AndKeepsTheHash()
    {
        var account = await AddAsync("ann");
        var stamp = Guid.NewGuid();

        var changed = await _repository.SignOutEverywhereAsync(account.Id, stamp, Ct);

        var read = await _repository.GetAsync(account.Id, Ct);
        Assert.True(changed);
        Assert.Equal("hash", read!.PasswordHash);
        Assert.Equal(stamp, read.SecurityStamp);
    }

    [Fact]
    public async Task Updating_AnUnknownAccount_ChangesNothing()
    {
        Assert.False(await _repository.ChangePasswordAsync(Guid.NewGuid(), "hash", Guid.NewGuid(), Ct));
        Assert.False(await _repository.SignOutEverywhereAsync(Guid.NewGuid(), Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task ADeletedAccount_IsGone_AndItsUsernameCanBeTakenAgain()
    {
        var account = await AddAsync("ann");

        var deleted = await _repository.DeleteAsync(account.Id, Ct);
        var deletedAgain = await _repository.DeleteAsync(account.Id, Ct);

        Assert.True(deleted);
        Assert.False(deletedAgain);
        Assert.Null(await _repository.GetAsync(account.Id, Ct));
        Assert.True(await _repository.TryAddAsync(NewAccount("ann"), Ct));
    }
}
