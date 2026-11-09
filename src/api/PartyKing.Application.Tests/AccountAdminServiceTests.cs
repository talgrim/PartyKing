using ErrorOr;
using PartyKing.Application.Accounts.Services;
using PartyKing.Domain.Entities.Auth;

namespace PartyKing.Application.Tests;

/// <summary>What an admin does with the users: the paged list and the password reset.</summary>
public class AccountAdminServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    private readonly FakeAccountsRepository _accounts = new();
    private readonly FakePasswordHasher _hasher = new();

    private AccountAdminService CreateService() => new(_accounts, _hasher);

    private Account AddAccount(string username, AccountRole role = AccountRole.User)
    {
        var account = Account.Create(
            Guid.NewGuid(),
            username,
            FakePasswordHasher.HashOf("old-password"),
            role,
            Guid.NewGuid(),
            Now).Account!;
        _accounts.Accounts.Add(account);

        return account;
    }

    [Fact]
    public async Task TheList_StartsOnPageOne_WithTheDefaultSize()
    {
        for (var i = 0; i < 25; i++)
        {
            AddAccount($"user{i:00}");
        }

        var page = await CreateService().ListUsersAsync(null, null, null, Ct);

        Assert.Equal(1, page.Page);
        Assert.Equal(IAccountAdminService.DefaultPageSize, page.PageSize);
        Assert.Equal(25, page.TotalCount);
        Assert.Equal(20, page.Items.Count);
        Assert.Equal("user00", page.Items[0].Username);
    }

    [Fact]
    public async Task TheList_ReturnsTheRestOnTheLastPage()
    {
        for (var i = 0; i < 25; i++)
        {
            AddAccount($"user{i:00}");
        }

        var page = await CreateService().ListUsersAsync(null, 2, null, Ct);

        Assert.Equal(5, page.Items.Count);
        Assert.Equal("user20", page.Items[0].Username);
    }

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-3, -1, 1, 1)]
    [InlineData(2, 5000, 2, IAccountAdminService.MaxPageSize)]
    [InlineData(3, 7, 3, 7)]
    public async Task TheList_KeepsThePageAndTheSizeInRange(int page, int pageSize, int expectedPage, int expectedSize)
    {
        var result = await CreateService().ListUsersAsync(null, page, pageSize, Ct);

        Assert.Equal(expectedPage, result.Page);
        Assert.Equal(expectedSize, result.PageSize);
        Assert.Equal(expectedPage, _accounts.LastPage);
        Assert.Equal(expectedSize, _accounts.LastPageSize);
    }

    [Fact]
    public async Task TheList_NeverContainsAnAdmin_OrAnySecret()
    {
        AddAccount("ann");
        AddAccount("boss", AccountRole.Admin);

        var page = await CreateService().ListUsersAsync(null, null, null, Ct);

        var item = Assert.Single(page.Items);
        Assert.Equal("ann", item.Username);
        Assert.Equal(PartyKing.Contract.V1.Enums.AccountRole.User, item.Role);
        Assert.Equal(Now, item.CreatedAt);
    }

    [Fact]
    public async Task TheList_CanBeNarrowedByASearch()
    {
        AddAccount("Anna");
        AddAccount("Hannah");
        AddAccount("bob");

        var page = await CreateService().ListUsersAsync("ANN", null, null, Ct);

        Assert.Equal(["Anna", "Hannah"], page.Items.Select(x => x.Username));
        Assert.Equal(2, page.TotalCount);
    }

    [Fact]
    public async Task Reset_SetsTheNewHashWithTheUserCost_AndSignsTheUserOutEverywhere()
    {
        var user = AddAccount("ann");
        var oldStamp = user.SecurityStamp;

        var result = await CreateService().ResetPasswordAsync(user.Id, "brand-new-password", Ct);

        Assert.False(result.IsError);
        Assert.Equal(FakePasswordHasher.HashOf("brand-new-password"), user.PasswordHash);
        Assert.Equal([PasswordHasher.UserIterations], _hasher.Iterations);
        Assert.NotEqual(oldStamp, user.SecurityStamp);
    }

    [Fact]
    public async Task Reset_OfAnAdmin_IsForbidden_AndChangesNothing()
    {
        var admin = AddAccount("boss", AccountRole.Admin);

        var result = await CreateService().ResetPasswordAsync(admin.Id, "brand-new-password", Ct);

        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
        Assert.Equal(FakePasswordHasher.HashOf("old-password"), admin.PasswordHash);
    }

    [Fact]
    public async Task Reset_OfAnUnknownAccount_IsNotFound()
    {
        var result = await CreateService().ResetPasswordAsync(Guid.NewGuid(), "brand-new-password", Ct);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("short")]
    public async Task Reset_ToAPasswordThatBreaksTheRule_IsAValidationError_AndChangesNothing(string? password)
    {
        var user = AddAccount("ann");

        var result = await CreateService().ResetPasswordAsync(user.Id, password, Ct);

        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal(FakePasswordHasher.HashOf("old-password"), user.PasswordHash);
    }
}
