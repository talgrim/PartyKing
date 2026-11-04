using PartyKing.Domain.Entities.Auth;

namespace PartyKing.Domain.Tests;

public class AdminAccountTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("admin", "admin")]
    [InlineData("  admin \t", "admin")]
    [InlineData("Ad Min", "Ad Min")]
    public void Usernames_AreTrimmed(string? username, string expected)
    {
        Assert.Equal(expected, AdminUsernames.Normalize(username));
    }

    [Theory]
    [InlineData(null, AdminAccountFailure.UsernameEmpty)]
    [InlineData("", AdminAccountFailure.UsernameEmpty)]
    [InlineData("  ", AdminAccountFailure.UsernameEmpty)]
    [InlineData("admin", AdminAccountFailure.None)]
    [InlineData(" a ", AdminAccountFailure.None)]
    public void Usernames_AreValidatedAfterTrimming(string? username, AdminAccountFailure expected)
    {
        Assert.Equal(expected, AdminUsernames.Validate(username));
    }

    [Fact]
    public void Create_KeepsTheIdAndHash_AndTheTrimmedUsername()
    {
        var id = Guid.NewGuid();

        var creation = AdminAccount.Create(id, "  admin ", "hash");

        Assert.Equal(AdminAccountFailure.None, creation.Failure);
        Assert.Equal(id, creation.Account!.Id);
        Assert.Equal("admin", creation.Account.Username);
        Assert.Equal("hash", creation.Account.PasswordHash);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithABlankUsername_IsRefused(string? username)
    {
        var creation = AdminAccount.Create(Guid.NewGuid(), username, "hash");

        Assert.Equal(AdminAccountFailure.UsernameEmpty, creation.Failure);
        Assert.Null(creation.Account);
    }
}
