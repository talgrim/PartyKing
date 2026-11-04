using PartyKing.Domain.Entities.Auth;

namespace PartyKing.Domain.Tests;

public class OAuthStateTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Issue_BindsTheStateToTheSession_AndLetsItLiveForTenMinutes()
    {
        var sessionId = Guid.NewGuid();

        var state = OAuthState.Issue(sessionId, Now);

        Assert.Equal(sessionId, state.SessionId);
        Assert.Equal(Now.AddMinutes(10), state.ExpiresAt);
        Assert.Equal(TimeSpan.FromMinutes(10), OAuthState.Lifetime);
    }

    [Fact]
    public void Issue_MakesA48CharacterHexId()
    {
        var state = OAuthState.Issue(Guid.NewGuid(), Now);

        Assert.Equal(48, state.Id.Length);
        Assert.All(state.Id, c => Assert.True(Uri.IsHexDigit(c)));
    }

    [Fact]
    public void Issue_NeverRepeatsAnId()
    {
        var ids = Enumerable.Range(0, 200).Select(_ => OAuthState.Issue(Guid.NewGuid(), Now).Id);

        Assert.Equal(200, ids.Distinct().Count());
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(599, false)]
    [InlineData(600, false)]
    [InlineData(601, true)]
    public void IsExpired_IsTrueOnlyAfterTheExpirationInstant(int secondsAfterIssue, bool expected)
    {
        var state = OAuthState.Issue(Guid.NewGuid(), Now);

        Assert.Equal(expected, state.IsExpired(Now.AddSeconds(secondsAfterIssue)));
    }
}
