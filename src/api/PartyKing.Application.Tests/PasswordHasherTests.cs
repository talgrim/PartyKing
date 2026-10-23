using PartyKing.Application.Admin.Services;

namespace PartyKing.Application.Tests;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.True(_hasher.Verify("correct horse battery staple", hash));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.False(_hasher.Verify("Correct horse battery staple", hash));
    }

    [Fact]
    public void Hash_SamePasswordTwice_UsesDifferentSalts()
    {
        Assert.NotEqual(_hasher.Hash("secret"), _hasher.Hash("secret"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plaintext")]
    [InlineData("pbkdf2$abc$def$ghi")]
    public void Verify_MalformedHash_ReturnsFalse(string hash)
    {
        Assert.False(_hasher.Verify("secret", hash));
    }

    [Fact]
    public void IsHash_GeneratedHash_ReturnsTrue()
    {
        Assert.True(_hasher.IsHash(_hasher.Hash("secret")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("jp2gmd")]
    [InlineData("pbkdf2$600000$not-base64!$also-not")]
    [InlineData("pbkdf2$0$AAAA$AAAA")]
    [InlineData("pbkdf2$600000$$")]
    public void IsHash_PlainTextOrMalformed_ReturnsFalse(string value)
    {
        Assert.False(_hasher.IsHash(value));
    }
}
