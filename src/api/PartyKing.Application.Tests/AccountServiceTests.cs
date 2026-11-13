using ErrorOr;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Accounts;
using PartyKing.Application.Accounts.Services;
using PartyKing.Domain.Entities.Auth;

namespace PartyKing.Application.Tests;

/// <summary>Register, login, the cookie check, changing the password, signing out everywhere and deleting: the rules are the Domain's, the hashing the fake hasher's.</summary>
public class AccountServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly FakeAccountsRepository _accounts = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeClock _clock = new();

    private AccountService CreateService(AccountDummyHash? dummyHash = null, DemoConfiguration? demo = null)
    {
        return new AccountService(
            _accounts,
            _hasher,
            dummyHash ?? new AccountDummyHash(_hasher),
            _clock,
            Options.Create(demo ?? new DemoConfiguration()));
    }

    private Account AddAccount(string username, string password, AccountRole role = AccountRole.User)
    {
        var account = Account.Create(
            Guid.NewGuid(),
            username,
            FakePasswordHasher.HashOf(password),
            role,
            Guid.NewGuid(),
            _clock.UtcNow.UtcDateTime).Account!;
        _accounts.Accounts.Add(account);

        return account;
    }

    /// <summary>
    /// The service is scoped and the account cookie handler builds it on every request that asks who the caller is, so
    /// building it must not hash anything: a hash takes tens of milliseconds of CPU, which every such request paid.
    /// </summary>
    [Fact]
    public void BuildingTheService_HashesNothing()
    {
        CreateService();
        CreateService();

        Assert.Equal(0, _hasher.Hashes);
    }

    [Fact]
    public async Task AnUnknownUsername_VerifiesAgainstOneDummyHashSharedByEveryService()
    {
        var dummyHash = new AccountDummyHash(_hasher);
        var first = CreateService(dummyHash);
        var second = CreateService(dummyHash);

        await first.LoginAsync("nobody", "secret-password", Ct);
        await second.LoginAsync("someone", "secret-password", Ct);

        Assert.Equal(1, _hasher.Hashes);
        Assert.Equal([PasswordHasher.UserIterations], _hasher.Iterations);
    }

    [Fact]
    public async Task Register_InTheDemo_IsRefused_AndMakesNoAccount()
    {
        var service = CreateService(demo: new DemoConfiguration { Enabled = true });

        var result = await service.RegisterAsync("ann", "secret-password", Ct);

        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
        Assert.Equal("Signing up is not available in the demo", result.FirstError.Description);
        Assert.Empty(_accounts.Accounts);
    }

    [Fact]
    public async Task Register_MakesAUser_HashedWithTheUserCost_AndReturnsWhoItIs()
    {
        var result = await CreateService().RegisterAsync("  ann ", "secret-password", Ct);

        var account = Assert.Single(_accounts.Accounts);
        Assert.Equal("ann", account.Username);
        Assert.Equal(AccountRole.User, account.Role);
        Assert.Equal(FakePasswordHasher.HashOf("secret-password"), account.PasswordHash);
        Assert.Equal(_clock.UtcNow.UtcDateTime, account.CreatedAt);
        Assert.Equal([PasswordHasher.UserIterations], _hasher.Iterations);
        Assert.Equal(new AccountIdentity(account.Id, AccountRole.User, account.SecurityStamp), result.Value);
    }

    [Theory]
    [InlineData("ANN")]
    [InlineData("ann")]
    [InlineData("  Ann ")]
    public async Task Register_WithATakenUsername_IsAConflict_WhateverTheCase(string username)
    {
        AddAccount("Ann", "secret-password");

        var result = await CreateService().RegisterAsync(username, "another-password", Ct);

        Assert.Equal(ErrorType.Conflict, result.FirstError.Type);
        Assert.Single(_accounts.Accounts);
    }

    [Theory]
    [InlineData(null, "secret-password")]
    [InlineData("   ", "secret-password")]
    [InlineData("ann", null)]
    [InlineData("ann", "short")]
    public async Task Register_WithABlankUsernameOrAPasswordThatBreaksTheRule_IsAValidationError(
        string? username,
        string? password)
    {
        var result = await CreateService().RegisterAsync(username, password, Ct);

        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Empty(_accounts.Accounts);
    }

    [Fact]
    public async Task Register_WithATooLongUsername_IsAValidationError()
    {
        var result = await CreateService().RegisterAsync(new string('a', AccountUsernames.MaxLength + 1), "secret-password", Ct);

        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Empty(_accounts.Accounts);
    }

    [Theory]
    [InlineData("ann")]
    [InlineData("  ANN ")]
    public async Task Login_WithTheRightCredentials_ReturnsWhoTheyAre(string username)
    {
        var account = AddAccount("ann", "secret-password");

        var result = await CreateService().LoginAsync(username, "secret-password", Ct);

        Assert.Equal(new AccountIdentity(account.Id, AccountRole.User, account.SecurityStamp), result.Value);
    }

    [Fact]
    public async Task Login_OfAnAdmin_ReturnsTheAdminRole()
    {
        AddAccount("boss", "secret-password", AccountRole.Admin);

        var result = await CreateService().LoginAsync("boss", "secret-password", Ct);

        Assert.Equal(AccountRole.Admin, result.Value.Role);
    }

    [Theory]
    [InlineData("ann", "wrong-password")]
    [InlineData("ann", null)]
    [InlineData("nobody", "secret-password")]
    [InlineData(null, "secret-password")]
    [InlineData("   ", "secret-password")]
    public async Task Login_WithAnythingElse_IsUnauthorized(string? username, string? password)
    {
        AddAccount("ann", "secret-password");

        var result = await CreateService().LoginAsync(username, password, Ct);

        Assert.Equal(ErrorType.Unauthorized, result.FirstError.Type);
    }

    [Fact]
    public async Task Login_WithAPasswordLongerThanAnyAccountCanHave_SpendsNoHash()
    {
        AddAccount("ann", "secret-password");

        var result = await CreateService().LoginAsync("ann", new string('a', AccountPassword.MaxLength + 1), Ct);

        Assert.Equal(ErrorType.Unauthorized, result.FirstError.Type);
        Assert.Equal(0, _hasher.Hashes);
    }

    [Fact]
    public async Task TheCookieCheck_AcceptsTheStampOfTheAccount_WithItsCurrentRole()
    {
        var account = AddAccount("ann", "secret-password");

        var identity = await CreateService().ValidateAsync(account.Id, account.SecurityStamp, Ct);

        Assert.Equal(new AccountIdentity(account.Id, AccountRole.User, account.SecurityStamp), identity);
    }

    [Fact]
    public async Task TheCookieCheck_RefusesAnotherStamp_AndADeletedAccount()
    {
        var account = AddAccount("ann", "secret-password");
        var service = CreateService();

        Assert.Null(await service.ValidateAsync(account.Id, Guid.NewGuid(), Ct));
        Assert.Null(await service.ValidateAsync(Guid.NewGuid(), account.SecurityStamp, Ct));
    }

    [Fact]
    public async Task Get_ReturnsTheAccountWithoutItsSecrets_OrNotFound()
    {
        var account = AddAccount("ann", "secret-password");
        var service = CreateService();

        var found = await service.GetAsync(account.Id, Ct);
        var missing = await service.GetAsync(Guid.NewGuid(), Ct);

        Assert.Equal(account.Id, found.Value.Id);
        Assert.Equal("ann", found.Value.Username);
        Assert.Equal(PartyKing.Contract.V1.Enums.AccountRole.User, found.Value.Role);
        Assert.Equal(account.CreatedAt, found.Value.CreatedAt);
        Assert.Equal(ErrorType.NotFound, missing.FirstError.Type);
    }

    [Fact]
    public async Task ChangePassword_WithTheRightCurrentOne_SetsTheNewHash_AndKillsTheOldCookies()
    {
        var account = AddAccount("ann", "secret-password");
        var oldStamp = account.SecurityStamp;

        var result = await CreateService().ChangePasswordAsync(account.Id, "secret-password", "brand-new-password", Ct);

        Assert.Equal(FakePasswordHasher.HashOf("brand-new-password"), account.PasswordHash);
        Assert.Equal([PasswordHasher.UserIterations], _hasher.Iterations);
        Assert.NotEqual(oldStamp, account.SecurityStamp);
        Assert.Equal(new AccountIdentity(account.Id, AccountRole.User, account.SecurityStamp), result.Value);
        Assert.Null(await CreateService().ValidateAsync(account.Id, oldStamp, Ct));
    }

    [Theory]
    [InlineData("wrong-password")]
    [InlineData(null)]
    public async Task ChangePassword_WithAWrongCurrentOne_IsAValidationError_AndChangesNothing(string? current)
    {
        var account = AddAccount("ann", "secret-password");
        var stamp = account.SecurityStamp;

        var result = await CreateService().ChangePasswordAsync(account.Id, current, "brand-new-password", Ct);

        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal(FakePasswordHasher.HashOf("secret-password"), account.PasswordHash);
        Assert.Equal(stamp, account.SecurityStamp);
    }

    [Fact]
    public async Task ChangePassword_ToOneThatBreaksTheRule_IsAValidationError_AndChangesNothing()
    {
        var account = AddAccount("ann", "secret-password");

        var result = await CreateService().ChangePasswordAsync(account.Id, "secret-password", "short", Ct);

        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal(FakePasswordHasher.HashOf("secret-password"), account.PasswordHash);
    }

    [Fact]
    public async Task ChangePassword_OfAnUnknownAccount_IsNotFound()
    {
        var result = await CreateService().ChangePasswordAsync(Guid.NewGuid(), "secret-password", "brand-new-password", Ct);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task SignOutEverywhere_ChangesTheStamp_AndKeepsThePassword()
    {
        var account = AddAccount("ann", "secret-password");
        var oldStamp = account.SecurityStamp;
        var service = CreateService();

        var result = await service.SignOutEverywhereAsync(account.Id, Ct);

        Assert.False(result.IsError);
        Assert.Equal(FakePasswordHasher.HashOf("secret-password"), account.PasswordHash);
        Assert.Null(await service.ValidateAsync(account.Id, oldStamp, Ct));
        Assert.NotNull(await service.LoginAsync("ann", "secret-password", Ct).ContinueWith(t => t.Result.IsError ? null : t.Result.Value));
    }

    [Fact]
    public async Task SignOutEverywhere_OfAnUnknownAccount_IsNotFound()
    {
        var result = await CreateService().SignOutEverywhereAsync(Guid.NewGuid(), Ct);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task Delete_WithTheRightPassword_RemovesTheAccount_AndItsCookieStopsWorking()
    {
        var account = AddAccount("ann", "secret-password");
        var service = CreateService();

        var result = await service.DeleteAsync(account.Id, "secret-password", Ct);

        Assert.False(result.IsError);
        Assert.Empty(_accounts.Accounts);
        Assert.Null(await service.ValidateAsync(account.Id, account.SecurityStamp, Ct));
    }

    [Theory]
    [InlineData("wrong-password")]
    [InlineData(null)]
    public async Task Delete_WithAWrongPassword_IsAValidationError_AndKeepsTheAccount(string? password)
    {
        var account = AddAccount("ann", "secret-password");

        var result = await CreateService().DeleteAsync(account.Id, password, Ct);

        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Contains(account, _accounts.Accounts);
    }

    [Fact]
    public async Task Delete_OfAnAdmin_IsForbidden_EvenWithTheRightPassword()
    {
        var admin = AddAccount("boss", "secret-password", AccountRole.Admin);

        var result = await CreateService().DeleteAsync(admin.Id, "secret-password", Ct);

        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
        Assert.Contains(admin, _accounts.Accounts);
    }

    [Fact]
    public async Task Delete_OfAnUnknownAccount_IsNotFound()
    {
        var result = await CreateService().DeleteAsync(Guid.NewGuid(), "secret-password", Ct);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }
}
