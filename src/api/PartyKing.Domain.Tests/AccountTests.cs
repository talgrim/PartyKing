using PartyKing.Domain.Entities.Auth;

namespace PartyKing.Domain.Tests;

public class AccountTests
{
    private static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    private static Account NewAccount(AccountRole role = AccountRole.User)
    {
        return Account.Create(Guid.NewGuid(), "ann", "hash", role, Guid.NewGuid(), Now).Account!;
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("admin", "admin")]
    [InlineData("  admin \t", "admin")]
    [InlineData("Ad Min", "Ad Min")]
    public void Usernames_AreTrimmed(string? username, string expected)
    {
        Assert.Equal(expected, AccountUsernames.Normalize(username));
    }

    [Theory]
    [InlineData(null, AccountFailure.UsernameEmpty)]
    [InlineData("", AccountFailure.UsernameEmpty)]
    [InlineData("  ", AccountFailure.UsernameEmpty)]
    [InlineData("admin", AccountFailure.None)]
    [InlineData(" a ", AccountFailure.None)]
    public void Usernames_AreValidatedAfterTrimming(string? username, AccountFailure expected)
    {
        Assert.Equal(expected, AccountUsernames.Validate(username));
    }

    [Fact]
    public void AUsernameOfTheMaximumLength_IsValid_AndOneCharacterMoreIsTooLong()
    {
        Assert.Equal(AccountFailure.None, AccountUsernames.Validate(new string('a', AccountUsernames.MaxLength)));
        Assert.Equal(AccountFailure.UsernameTooLong, AccountUsernames.Validate(new string('a', AccountUsernames.MaxLength + 1)));
    }

    [Fact]
    public void TheMaximumLength_IsCountedAfterTrimming()
    {
        var padded = $"  {new string('a', AccountUsernames.MaxLength)}  ";

        Assert.Equal(AccountFailure.None, AccountUsernames.Validate(padded));
    }

    [Fact]
    public void Create_TakesEverythingItIsGiven_AndTrimsTheUsername()
    {
        var id = Guid.NewGuid();
        var stamp = Guid.NewGuid();

        var creation = Account.Create(id, "  admin ", "hash", AccountRole.Admin, stamp, Now);

        Assert.Equal(AccountFailure.None, creation.Failure);
        Assert.Equal(id, creation.Account!.Id);
        Assert.Equal("admin", creation.Account.Username);
        Assert.Equal("hash", creation.Account.PasswordHash);
        Assert.Equal(AccountRole.Admin, creation.Account.Role);
        Assert.Equal(stamp, creation.Account.SecurityStamp);
        Assert.Equal(Now, creation.Account.CreatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithABlankUsername_IsRefused(string? username)
    {
        var creation = Account.Create(Guid.NewGuid(), username, "hash", AccountRole.User, Guid.NewGuid(), Now);

        Assert.Equal(AccountFailure.UsernameEmpty, creation.Failure);
        Assert.Null(creation.Account);
    }

    [Fact]
    public void Create_WithATooLongUsername_IsRefused()
    {
        var username = new string('a', AccountUsernames.MaxLength + 1);

        var creation = Account.Create(Guid.NewGuid(), username, "hash", AccountRole.User, Guid.NewGuid(), Now);

        Assert.Equal(AccountFailure.UsernameTooLong, creation.Failure);
        Assert.Null(creation.Account);
    }

    [Fact]
    public void ACookieWithTheAccountsStamp_SignsItIn_AndAnyOtherStampDoesNot()
    {
        var account = NewAccount();

        Assert.True(account.IsSignedInBy(account.SecurityStamp));
        Assert.False(account.IsSignedInBy(Guid.NewGuid()));
    }

    [Fact]
    public void ChangePassword_ReplacesTheHash_AndTheStampSoOldCookiesStopWorking()
    {
        var account = NewAccount();
        var oldStamp = account.SecurityStamp;
        var newStamp = Guid.NewGuid();

        account.ChangePassword("new-hash", newStamp);

        Assert.Equal("new-hash", account.PasswordHash);
        Assert.Equal(newStamp, account.SecurityStamp);
        Assert.False(account.IsSignedInBy(oldStamp));
        Assert.True(account.IsSignedInBy(newStamp));
    }

    [Fact]
    public void SignOutEverywhere_ReplacesOnlyTheStamp()
    {
        var account = NewAccount();
        var oldStamp = account.SecurityStamp;
        var newStamp = Guid.NewGuid();

        account.SignOutEverywhere(newStamp);

        Assert.Equal("hash", account.PasswordHash);
        Assert.False(account.IsSignedInBy(oldStamp));
        Assert.True(account.IsSignedInBy(newStamp));
    }
}
