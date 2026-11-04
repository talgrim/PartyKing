using PartyKing.Domain.Entities.Sessions;

namespace PartyKing.Domain.Tests;

public class SessionAccessTests
{
    private static Session NewSession(string passwordHash = "hash", string? hostProfileId = null)
    {
        var session = Session.Create(Guid.NewGuid(), "ABC123", "Party", passwordHash, DateTime.UtcNow).Session!;
        session.HostProfileId = hostProfileId;

        return session;
    }

    [Fact]
    public void AccessStamp_IsStable_AndDoesNotRevealTheHash()
    {
        var session = NewSession("secret-hash");

        Assert.Equal(session.AccessStamp, NewSession("secret-hash").AccessStamp);
        Assert.Equal(16, session.AccessStamp.Length);
        Assert.DoesNotContain("secret-hash", session.AccessStamp);
    }

    [Fact]
    public void AccessStamp_ChangesWithThePassword_SoOldUnlocksStopWorking()
    {
        var session = NewSession("old");
        var oldStamp = session.AccessStamp;

        session.ChangePassword("new");

        Assert.NotEqual(oldStamp, session.AccessStamp);
        Assert.False(session.IsUnlockedBy(oldStamp));
        Assert.True(session.IsUnlockedBy(session.AccessStamp));
    }

    [Fact]
    public void AccessStampOf_IsTheStampASessionWithThatHashHas()
    {
        Assert.Equal(NewSession("new-hash").AccessStamp, Session.AccessStampOf("new-hash"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0000000000000000")]
    public void IsUnlockedBy_RefusesAMissingOrWrongStamp(string? stamp)
    {
        Assert.False(NewSession().IsUnlockedBy(stamp));
    }

    [Fact]
    public void IsHostedBy_IsTrueOnlyForTheHost()
    {
        var session = NewSession(hostProfileId: "host-1");

        Assert.True(session.IsHostedBy("host-1"));
        Assert.False(session.IsHostedBy("someone-else"));
        Assert.False(session.IsHostedBy(null));
    }

    [Fact]
    public void IsHostedBy_NobodyHostsASessionWithoutAHost_NotEvenAnAnonymousCaller()
    {
        var session = NewSession();

        Assert.False(session.IsHostedBy(null));
        Assert.False(session.IsHostedBy("host-1"));
    }

    [Theory]
    [InlineData("host-1", false, true)]
    [InlineData("someone-else", false, false)]
    [InlineData(null, false, false)]
    [InlineData(null, true, true)]
    [InlineData("someone-else", true, true)]
    public void IsManagedBy_IsTheHostOrAnAdmin(string? profileId, bool isAdmin, bool expected)
    {
        var session = NewSession(hostProfileId: "host-1");

        Assert.Equal(expected, session.IsManagedBy(profileId, isAdmin));
    }
}
