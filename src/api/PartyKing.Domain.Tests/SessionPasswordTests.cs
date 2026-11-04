using PartyKing.Domain.Entities.Sessions;

namespace PartyKing.Domain.Tests;

public class SessionPasswordTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    public void Create_RefusesAPasswordUnderTheMinimum(string? password)
    {
        var creation = SessionPassword.Create(password);

        Assert.Equal(SessionPasswordFailure.TooShort, creation.Failure);
        Assert.Null(creation.Password);
    }

    [Fact]
    public void Create_RefusesAPasswordOverTheMaximum()
    {
        var creation = SessionPassword.Create(new string('a', SessionPassword.MaxLength + 1));

        Assert.Equal(SessionPasswordFailure.TooLong, creation.Failure);
        Assert.Null(creation.Password);
    }

    [Theory]
    [InlineData(SessionPassword.MinLength)]
    [InlineData(SessionPassword.MaxLength)]
    public void Create_AcceptsTheBoundaryLengths(int length)
    {
        var typed = new string('a', length);

        var creation = SessionPassword.Create(typed);

        Assert.Equal(SessionPasswordFailure.None, creation.Failure);
        Assert.Equal(typed, creation.Password!.Value);
    }

    [Fact]
    public void Create_KeepsSpacesAsTyped()
    {
        Assert.Equal("  ab  ", SessionPassword.Create("  ab  ").Password!.Value);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", true)]
    [InlineData("anything short", true)]
    public void CanMatch_OnlyTurnsAwayAMissingPasswordOrAnOverlongOne(string? password, bool expected)
    {
        Assert.Equal(expected, SessionPassword.CanMatch(password));
    }

    [Fact]
    public void CanMatch_TurnsAwayAPasswordOverTheMaximum()
    {
        Assert.False(SessionPassword.CanMatch(new string('a', SessionPassword.MaxLength + 1)));
        Assert.True(SessionPassword.CanMatch(new string('a', SessionPassword.MaxLength)));
    }
}
