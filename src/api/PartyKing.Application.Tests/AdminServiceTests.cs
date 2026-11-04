using ErrorOr;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PartyKing.Application.Admin.Services;
using PartyKing.Application.Configuration;
using PartyKing.Domain.Entities.Auth;
using PartyKing.Domain.Repositories;

namespace PartyKing.Application.Tests;

/// <summary>Admin login and seeding: the username rule is the aggregate's, the hashing the fake hasher's.</summary>
public class AdminServiceTests
{
    private readonly FakeAccountsRepository _accounts = new();

    private AdminService CreateService(AdminConfiguration? configuration = null)
    {
        return new AdminService(
            _accounts,
            new FakePasswordHasher(),
            Options.Create(configuration ?? new AdminConfiguration()),
            NullLogger<AdminService>.Instance);
    }

    private Guid AddAccount(string username, string password)
    {
        var account = AdminAccount.Create(Guid.NewGuid(), username, FakePasswordHasher.HashOf(password)).Account!;
        _accounts.Accounts.Add(account);

        return account.Id;
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("  admin ")]
    public async Task Login_WithTheRightCredentials_ReturnsTheAccountId(string username)
    {
        var id = AddAccount("admin", "secret");

        var result = await CreateService().LoginAsync(username, "secret", CancellationToken.None);

        Assert.Equal(id, result.Value);
    }

    [Theory]
    [InlineData("admin", "wrong")]
    [InlineData("admin", null)]
    [InlineData("nobody", "secret")]
    [InlineData(null, "secret")]
    [InlineData("   ", "secret")]
    public async Task Login_WithAnythingElse_IsUnauthorized(string? username, string? password)
    {
        AddAccount("admin", "secret");

        var result = await CreateService().LoginAsync(username, password, CancellationToken.None);

        Assert.Equal(ErrorType.Unauthorized, result.FirstError.Type);
    }

    [Fact]
    public async Task Seed_CreatesTheAccountUnderTheTrimmedUsername_WithTheHashedPassword()
    {
        var service = CreateService(new AdminConfiguration { Username = "  boss ", Password = "secret" });

        await service.SeedAsync(CancellationToken.None);

        var account = Assert.Single(_accounts.Accounts);
        Assert.Equal("boss", account.Username);
        Assert.Equal(FakePasswordHasher.HashOf("secret"), account.PasswordHash);
        Assert.NotEqual(Guid.Empty, account.Id);
    }

    [Fact]
    public async Task Seed_WithAPasswordHash_KeepsTheTrimmedHash()
    {
        var hash = FakePasswordHasher.HashOf("secret");
        var service = CreateService(new AdminConfiguration { Username = "boss", PasswordHash = $" {hash} " });

        await service.SeedAsync(CancellationToken.None);

        Assert.Equal(hash, Assert.Single(_accounts.Accounts).PasswordHash);
    }

    [Fact]
    public async Task Seed_WhenAnAccountExists_DoesNothing()
    {
        AddAccount("admin", "secret");
        var service = CreateService(new AdminConfiguration { Username = "boss", Password = "other" });

        await service.SeedAsync(CancellationToken.None);

        Assert.Equal("admin", Assert.Single(_accounts.Accounts).Username);
    }

    [Theory]
    [InlineData(null, "secret", null)]
    [InlineData("   ", "secret", null)]
    [InlineData("boss", null, null)]
    [InlineData("boss", "  ", null)]
    [InlineData("boss", null, "not-a-hash")]
    public async Task Seed_WithIncompleteOrInvalidSettings_CreatesNothing(string? username, string? password, string? hash)
    {
        var service = CreateService(new AdminConfiguration { Username = username, Password = password, PasswordHash = hash });

        await service.SeedAsync(CancellationToken.None);

        Assert.Empty(_accounts.Accounts);
    }

    private sealed class FakeAccountsRepository : IAdminAccountsRepository
    {
        public List<AdminAccount> Accounts { get; } = [];

        public Task<bool> AnyAsync(CancellationToken cancellationToken) => Task.FromResult(Accounts.Count > 0);

        public Task<AdminAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken) =>
            Task.FromResult(Accounts.FirstOrDefault(x => x.Username == username));

        public Task<AdminAccount?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Accounts.FirstOrDefault(x => x.Id == id));

        public Task AddAsync(AdminAccount account, CancellationToken cancellationToken)
        {
            Accounts.Add(account);
            return Task.CompletedTask;
        }
    }

    /// <summary>The real hasher takes 600 000 PBKDF2 iterations a hash, which a unit test of the flow does not need.</summary>
    private sealed class FakePasswordHasher : IPasswordHasher
    {
        private const string Prefix = "fake$";

        public static string HashOf(string password) => Prefix + password;

        public string Hash(string password, int iterations = PasswordHasher.AdminIterations) => HashOf(password);

        public bool Verify(string password, string hash) => hash == HashOf(password);

        public bool IsHash(string value) => value.StartsWith(Prefix, StringComparison.Ordinal);
    }
}
