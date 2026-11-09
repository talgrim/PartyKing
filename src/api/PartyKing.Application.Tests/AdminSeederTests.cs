using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PartyKing.Application.Accounts.Services;
using PartyKing.Application.Configuration;
using PartyKing.Domain.Entities.Auth;

namespace PartyKing.Application.Tests;

/// <summary>Seeding the first admin from configuration: the username rule is the Domain's, the hashing the fake hasher's.</summary>
public class AdminSeederTests
{
    private readonly FakeAccountsRepository _accounts = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeClock _clock = new();

    private AdminSeeder CreateSeeder(AdminConfiguration configuration)
    {
        return new AdminSeeder(
            _accounts,
            _hasher,
            _clock,
            Options.Create(configuration),
            NullLogger<AdminSeeder>.Instance);
    }

    private void AddAccount(string username, AccountRole role)
    {
        _accounts.Accounts.Add(Account.Create(
            Guid.NewGuid(),
            username,
            FakePasswordHasher.HashOf("secret"),
            role,
            Guid.NewGuid(),
            _clock.UtcNow.UtcDateTime).Account!);
    }

    [Fact]
    public async Task Seed_CreatesAnAdminUnderTheTrimmedUsername_WithTheHashedPassword()
    {
        var seeder = CreateSeeder(new AdminConfiguration { Username = "  boss ", Password = "secret" });

        await seeder.SeedAsync(CancellationToken.None);

        var account = Assert.Single(_accounts.Accounts);
        Assert.Equal("boss", account.Username);
        Assert.Equal(AccountRole.Admin, account.Role);
        Assert.Equal(FakePasswordHasher.HashOf("secret"), account.PasswordHash);
        Assert.NotEqual(Guid.Empty, account.Id);
        Assert.NotEqual(Guid.Empty, account.SecurityStamp);
        Assert.Equal(_clock.UtcNow.UtcDateTime, account.CreatedAt);
    }

    [Fact]
    public async Task Seed_KeepsTheAdminCostForAPlainPassword()
    {
        var seeder = CreateSeeder(new AdminConfiguration { Username = "boss", Password = "secret" });

        await seeder.SeedAsync(CancellationToken.None);

        Assert.Equal([PasswordHasher.AdminIterations], _hasher.Iterations);
    }

    [Fact]
    public async Task Seed_WithAPasswordHash_KeepsTheTrimmedHash()
    {
        var hash = FakePasswordHasher.HashOf("secret");
        var seeder = CreateSeeder(new AdminConfiguration { Username = "boss", PasswordHash = $" {hash} " });

        await seeder.SeedAsync(CancellationToken.None);

        Assert.Equal(hash, Assert.Single(_accounts.Accounts).PasswordHash);
    }

    [Fact]
    public async Task Seed_WhenAnAdminExists_DoesNothing()
    {
        AddAccount("admin", AccountRole.Admin);
        var seeder = CreateSeeder(new AdminConfiguration { Username = "boss", Password = "other" });

        await seeder.SeedAsync(CancellationToken.None);

        Assert.Equal("admin", Assert.Single(_accounts.Accounts).Username);
    }

    [Fact]
    public async Task Seed_WhenOnlyUsersExist_StillCreatesTheAdmin()
    {
        AddAccount("ann", AccountRole.User);
        var seeder = CreateSeeder(new AdminConfiguration { Username = "boss", Password = "secret" });

        await seeder.SeedAsync(CancellationToken.None);

        Assert.Equal(2, _accounts.Accounts.Count);
        Assert.Contains(_accounts.Accounts, x => x.Username == "boss" && x.Role == AccountRole.Admin);
    }

    [Fact]
    public async Task Seed_WhenAUserHasTakenTheConfiguredUsername_CreatesNothing_AndDoesNotThrow()
    {
        AddAccount("Boss", AccountRole.User);
        var seeder = CreateSeeder(new AdminConfiguration { Username = "boss", Password = "secret" });

        await seeder.SeedAsync(CancellationToken.None);

        var account = Assert.Single(_accounts.Accounts);
        Assert.Equal(AccountRole.User, account.Role);
    }

    [Theory]
    [InlineData(null, "secret", null)]
    [InlineData("   ", "secret", null)]
    [InlineData("boss", null, null)]
    [InlineData("boss", "  ", null)]
    [InlineData("boss", null, "not-a-hash")]
    public async Task Seed_WithIncompleteOrInvalidSettings_CreatesNothing(string? username, string? password, string? hash)
    {
        var seeder = CreateSeeder(new AdminConfiguration { Username = username, Password = password, PasswordHash = hash });

        await seeder.SeedAsync(CancellationToken.None);

        Assert.Empty(_accounts.Accounts);
    }
}
