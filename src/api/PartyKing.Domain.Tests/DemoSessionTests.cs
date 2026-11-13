using PartyKing.Domain.Entities.Demo;

namespace PartyKing.Domain.Tests;

public class DemoSessionTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Start_TiesTheHostToTheSession_AndEndsItAfterTheLifetime()
    {
        var sessionId = Guid.NewGuid();

        var demoSession = DemoSession.Start(sessionId, "demo-host", Now, TimeSpan.FromHours(3));

        Assert.Equal(sessionId, demoSession.SessionId);
        Assert.Equal("demo-host", demoSession.HostProfileId);
        Assert.Equal(Now.AddHours(3), demoSession.ExpiresAt);
    }

    [Fact]
    public void ASession_IsNotOverBeforeItsExpiration()
    {
        var demoSession = DemoSession.Start(Guid.NewGuid(), "demo-host", Now, TimeSpan.FromHours(3));

        Assert.False(demoSession.IsExpired(Now));
        Assert.False(demoSession.IsExpired(Now.AddHours(3).AddTicks(-1)));
    }

    [Fact]
    public void ASession_IsOverFromItsExpirationInstantOn()
    {
        var demoSession = DemoSession.Start(Guid.NewGuid(), "demo-host", Now, TimeSpan.FromHours(3));

        Assert.True(demoSession.IsExpired(Now.AddHours(3)));
        Assert.True(demoSession.IsExpired(Now.AddHours(4)));
    }
}
