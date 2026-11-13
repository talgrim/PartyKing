using ErrorOr;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Demo.Models;
using PartyKing.Application.Demo.Services;
using PartyKing.Domain.Enums;

namespace PartyKing.Application.Tests;

public class DemoSessionServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly FakeSessionsRepository _sessions = new();
    private readonly FakeSpotifyProfilesRepository _profiles = new();
    private readonly FakeDemoSessionsRepository _demoSessions = new();
    private readonly FakeSlideshowService _slideshows = new();
    private readonly FakeClock _clock = new();

    private DemoSessionService CreateService(DemoConfiguration? configuration = null)
    {
        return new DemoSessionService(
            Options.Create(configuration ?? new DemoConfiguration { Enabled = true }),
            SessionTestServices.Admin(_sessions, slideshows: _slideshows),
            _sessions,
            _profiles,
            _demoSessions,
            _clock,
            NullLogger<DemoSessionService>.Instance);
    }

    [Fact]
    public async Task Start_MakesASessionHostedByASyntheticProfile()
    {
        var result = await CreateService().StartAsync(Ct);

        var start = result.Value;
        var session = await _sessions.GetAsync(start.SessionId, Ct);
        Assert.Equal(start.ProfileId, session!.HostProfileId);
        Assert.StartsWith("demo-", start.ProfileId);
        var profile = Assert.Single(_profiles.Profiles).Value;
        Assert.Equal(start.ProfileId, profile.ProfileId);
        Assert.Equal("Demo host", profile.DisplayName);
    }

    [Fact]
    public async Task Start_HandsOverTheStampThatUnlocksTheSession_SoNoPasswordIsNeeded()
    {
        var start = (await CreateService().StartAsync(Ct)).Value;

        var session = await _sessions.GetAsync(start.SessionId, Ct);

        Assert.Equal(session!.AccessStamp, start.AccessStamp);
        Assert.True(session.IsUnlockedBy(start.AccessStamp));
    }

    [Fact]
    public async Task Start_EndsTheSessionAfterTheConfiguredLifetime()
    {
        var configuration = new DemoConfiguration { Enabled = true, LifetimeMinutes = 90 };

        var start = (await CreateService(configuration).StartAsync(Ct)).Value;

        var demoSession = Assert.Single(_demoSessions.Items);
        Assert.Equal(start.SessionId, demoSession.SessionId);
        Assert.Equal(start.ProfileId, demoSession.HostProfileId);
        Assert.Equal(_clock.UtcNow.UtcDateTime.AddMinutes(90), demoSession.ExpiresAt);
        Assert.Equal(_clock.UtcNow.AddMinutes(90), start.ExpiresAt);
    }

    [Fact]
    public async Task Start_ForTwoVisitors_MakesTwoSessionsWithTheirOwnHosts()
    {
        var service = CreateService();

        var first = (await service.StartAsync(Ct)).Value;
        var second = (await service.StartAsync(Ct)).Value;

        Assert.NotEqual(first.SessionId, second.SessionId);
        Assert.NotEqual(first.ProfileId, second.ProfileId);
        // The refresh token of a profile is unique in the database
        Assert.Equal(2, _profiles.Profiles.Values.Select(x => x.RefreshToken).Distinct().Count());
    }

    [Fact]
    public async Task Start_WhenTheServerIsNotADemo_IsNotFound_AndMakesNothing()
    {
        var result = await CreateService(new DemoConfiguration { Enabled = false }).StartAsync(Ct);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
        Assert.Empty(_profiles.Profiles);
        Assert.Empty(_demoSessions.Items);
    }

    [Fact]
    public async Task Start_WhenTheDemoIsFull_IsAConflict_AndMakesNothingMore()
    {
        var service = CreateService(new DemoConfiguration { Enabled = true, MaxSessions = 1 });
        await service.StartAsync(Ct);

        var result = await service.StartAsync(Ct);

        Assert.Equal(ErrorType.Conflict, result.FirstError.Type);
        Assert.Single(_demoSessions.Items);
        Assert.Single(_profiles.Profiles);
    }

    [Fact]
    public async Task Start_WhenTheSessionCannotBeClaimed_TakesBackTheSessionAndTheProfile()
    {
        _sessions.ForcedClaimResult = SessionClaimResult.AlreadyClaimed;

        var result = await CreateService().StartAsync(Ct);

        Assert.True(result.IsError);
        Assert.Single(_slideshows.DeletedSessions);
        Assert.Empty(_profiles.Profiles);
        Assert.Single(_profiles.Deleted);
    }

    [Fact]
    public async Task Start_WhenItFailsHalfWay_TakesBackWhatItMade_AndLetsTheFailureThrough()
    {
        _demoSessions.AddFailure = new InvalidOperationException("the database is gone");

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().StartAsync(Ct));

        Assert.Single(_slideshows.DeletedSessions);
        Assert.Empty(_profiles.Profiles);
    }

    [Fact]
    public async Task Get_AnswersWhenTheSessionEnds_AndTheServersClock()
    {
        var service = CreateService();
        var start = (await service.StartAsync(Ct)).Value;
        _clock.Advance(TimeSpan.FromMinutes(10));

        var result = await service.GetAsync(start.SessionId, Ct);

        Assert.Equal(start.ExpiresAt, result.Value.ExpiresAt);
        Assert.Equal(_clock.UtcNow, result.Value.ServerTime);
    }

    [Fact]
    public async Task ListStarted_ListsTheVisitorsOwnSessionsWithTheirEnd_TheLastEndingFirst()
    {
        var service = CreateService();
        var first = (await service.StartAsync(Ct)).Value;
        _clock.Advance(TimeSpan.FromMinutes(10));
        var second = (await service.StartAsync(Ct)).Value;

        var result = await service.ListStartedAsync(
            [new StartedDemoSession(first.SessionId, first.ProfileId), new StartedDemoSession(second.SessionId, second.ProfileId)],
            Ct);

        Assert.Equal([second.SessionId, first.SessionId], result.Value.Sessions.Select(x => x.SessionId));
        Assert.Equal(first.ExpiresAt, result.Value.Sessions[1].ExpiresAt);
        Assert.Equal("Demo party", result.Value.Sessions[0].Name);
        Assert.Equal(_clock.UtcNow, result.Value.ServerTime);
    }

    [Fact]
    public async Task ListStarted_SkipsWhatTheVisitorDidNotStartEndedOrUnknownSessions()
    {
        var service = CreateService();
        var mine = (await service.StartAsync(Ct)).Value;
        var theirs = (await service.StartAsync(Ct)).Value;
        var ordinary = _sessions.AddSession();

        var result = await service.ListStartedAsync(
            [
                new StartedDemoSession(mine.SessionId, mine.ProfileId),
                new StartedDemoSession(theirs.SessionId, mine.ProfileId),
                new StartedDemoSession(ordinary.Id, mine.ProfileId),
                new StartedDemoSession(Guid.NewGuid(), mine.ProfileId),
            ],
            Ct);

        Assert.Equal([mine.SessionId], result.Value.Sessions.Select(x => x.SessionId));
    }

    [Fact]
    public async Task ListStarted_DropsASessionThatEndedEvenBeforeTheSweepDeletedIt()
    {
        var service = CreateService(new DemoConfiguration { Enabled = true, LifetimeMinutes = 30 });
        var start = (await service.StartAsync(Ct)).Value;
        _clock.Advance(TimeSpan.FromMinutes(30));

        var result = await service.ListStartedAsync([new StartedDemoSession(start.SessionId, start.ProfileId)], Ct);

        Assert.Empty(result.Value.Sessions);
    }

    [Fact]
    public async Task ListStarted_WhenTheServerIsNotADemo_IsNotFound()
    {
        var result = await CreateService(new DemoConfiguration { Enabled = false }).ListStartedAsync([], Ct);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task Resume_HandsOverWhatAStartDoes_WithTheSameEnd()
    {
        var service = CreateService(new DemoConfiguration { Enabled = true, LifetimeMinutes = 60 });
        var start = (await service.StartAsync(Ct)).Value;
        _clock.Advance(TimeSpan.FromMinutes(20));

        var resumed = (await service.ResumeAsync(new StartedDemoSession(start.SessionId, start.ProfileId), Ct)).Value;

        Assert.Equal(start.SessionId, resumed.SessionId);
        Assert.Equal(start.ProfileId, resumed.ProfileId);
        Assert.Equal(start.AccessStamp, resumed.AccessStamp);
        Assert.Equal(start.ExpiresAt, resumed.ExpiresAt);
    }

    [Fact]
    public async Task Resume_ForAnotherHostsSession_IsNotFound()
    {
        var service = CreateService();
        var mine = (await service.StartAsync(Ct)).Value;
        var theirs = (await service.StartAsync(Ct)).Value;

        var result = await service.ResumeAsync(new StartedDemoSession(theirs.SessionId, mine.ProfileId), Ct);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task Resume_OfASessionThatEnded_IsNotFound()
    {
        var service = CreateService(new DemoConfiguration { Enabled = true, LifetimeMinutes = 30 });
        var start = (await service.StartAsync(Ct)).Value;
        _clock.Advance(TimeSpan.FromMinutes(31));

        var result = await service.ResumeAsync(new StartedDemoSession(start.SessionId, start.ProfileId), Ct);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task Get_ForASessionThatIsNotADemoOne_IsNotFound()
    {
        var ordinary = _sessions.AddSession();

        var result = await CreateService().GetAsync(ordinary.Id, Ct);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task Get_WhenTheServerIsNotADemo_IsNotFound()
    {
        var result = await CreateService(new DemoConfiguration { Enabled = false }).GetAsync(Guid.NewGuid(), Ct);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Config_SaysWhetherTheServerIsADemo_WithItsClock(bool enabled)
    {
        var config = CreateService(new DemoConfiguration { Enabled = enabled }).GetConfig();

        Assert.Equal(enabled, config.Enabled);
        Assert.Equal(_clock.UtcNow, config.ServerTime);
    }
}
