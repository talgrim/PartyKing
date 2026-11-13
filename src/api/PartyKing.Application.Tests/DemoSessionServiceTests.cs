using ErrorOr;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Demo.Models;
using PartyKing.Application.Demo.Services;

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
            _sessions,
            _demoSessions,
            _clock);
    }

    // The sessions are started the way the controller does, by the start service on the same fakes
    private Task<ErrorOr<DemoStart>> StartAsync(DemoConfiguration? configuration = null)
    {
        return new DemoSessionStartService(
            Options.Create(configuration ?? new DemoConfiguration { Enabled = true }),
            SessionTestServices.Admin(_sessions, slideshows: _slideshows),
            _sessions,
            _profiles,
            _demoSessions,
            _clock,
            NullLogger<DemoSessionStartService>.Instance).StartAsync(Ct);
    }

    [Fact]
    public async Task Get_AnswersWhenTheSessionEnds_AndTheServersClock()
    {
        var service = CreateService();
        var start = (await StartAsync()).Value;
        _clock.Advance(TimeSpan.FromMinutes(10));

        var result = await service.GetAsync(start.SessionId, Ct);

        Assert.Equal(start.ExpiresAt, result.Value.ExpiresAt);
        Assert.Equal(_clock.UtcNow, result.Value.ServerTime);
    }

    [Fact]
    public async Task ListStarted_ListsTheVisitorsOwnSessionsWithTheirEnd_TheLastEndingFirst()
    {
        var service = CreateService();
        var first = (await StartAsync()).Value;
        _clock.Advance(TimeSpan.FromMinutes(10));
        var second = (await StartAsync()).Value;

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
        var mine = (await StartAsync()).Value;
        var theirs = (await StartAsync()).Value;
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
        var configuration = new DemoConfiguration { Enabled = true, LifetimeMinutes = 30 };
        var service = CreateService(configuration);
        var start = (await StartAsync(configuration)).Value;
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
        var configuration = new DemoConfiguration { Enabled = true, LifetimeMinutes = 60 };
        var service = CreateService(configuration);
        var start = (await StartAsync(configuration)).Value;
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
        var mine = (await StartAsync()).Value;
        var theirs = (await StartAsync()).Value;

        var result = await service.ResumeAsync(new StartedDemoSession(theirs.SessionId, mine.ProfileId), Ct);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task Resume_OfASessionThatEnded_IsNotFound()
    {
        var configuration = new DemoConfiguration { Enabled = true, LifetimeMinutes = 30 };
        var service = CreateService(configuration);
        var start = (await StartAsync(configuration)).Value;
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
}
