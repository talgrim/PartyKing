using PartyKing.Domain.Entities.Auth;

namespace PartyKing.Domain.Tests;

public class AccountPasswordTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567")]
    public void APasswordUnderTheMinimum_IsTooShort(string? password)
    {
        var creation = AccountPassword.Create(password);

        Assert.Equal(AccountPasswordFailure.TooShort, creation.Failure);
        Assert.Null(creation.Password);
    }

    [Fact]
    public void APasswordOverTheMaximum_IsTooLong()
    {
        var creation = AccountPassword.Create(new string('a', AccountPassword.MaxLength + 1));

        Assert.Equal(AccountPasswordFailure.TooLong, creation.Failure);
        Assert.Null(creation.Password);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(100)]
    public void APasswordOnTheLimits_IsAccepted(int length)
    {
        var creation = AccountPassword.Create(new string('a', length));

        Assert.Equal(AccountPasswordFailure.None, creation.Failure);
        Assert.Equal(length, creation.Password!.Value.Length);
    }

    [Fact]
    public void APassword_IsNotTrimmed()
    {
        var creation = AccountPassword.Create("   secret   ");

        Assert.Equal("   secret   ", creation.Password!.Value);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("short", true)]
    [InlineData(null, false)]
    public void ATypedPasswordNotLongerThanTheMaximum_CanMatch_AndAMissingOneCannot(string? password, bool expected)
    {
        Assert.Equal(expected, AccountPassword.CanMatch(password));
    }

    [Fact]
    public void ATypedPasswordLongerThanTheMaximum_CannotMatch()
    {
        Assert.False(AccountPassword.CanMatch(new string('a', AccountPassword.MaxLength + 1)));
    }
}
