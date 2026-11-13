using System.Reflection;
using ErrorOr;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.API.Authorization;
using PartyKing.API.Controllers;
using PartyKing.API.Cookies;
using PartyKing.Application.Demo.Models;
using PartyKing.Application.Demo.Services;
using PartyKing.Contract.V1.Demo;

namespace PartyKing.Api.Tests;

/// <summary>The demo endpoints: starting a session sets the cookies of a host who knows the password, and nothing else does.</summary>
public class DemoControllerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeDemoSessionService _service = new();
    private readonly SignedCookies _cookies = new(DataProtectionProvider.Create("partyking-tests"));
    private readonly DefaultHttpContext _httpContext;
    private readonly DemoController _controller;

    public DemoControllerTests()
    {
        var services = new ServiceCollection().AddLogging().AddControllers().Services.BuildServiceProvider();
        _httpContext = new DefaultHttpContext { RequestServices = services };
        _controller = new DemoController(
            new HttpContextAccessor { HttpContext = _httpContext },
            _service,
            _service,
            _service,
            _cookies)
        {
            ControllerContext = new ControllerContext { HttpContext = _httpContext }
        };
    }

    private readonly Dictionary<string, string> _jar = [];

    /// <summary>The browser keeps what the server set and sends it with its next request.</summary>
    private void Roundtrip()
    {
        foreach (var parts in _httpContext.Response.Headers.SetCookie.Select(x => x!.Split(';')[0].Split('=', 2)))
        {
            _jar[parts[0]] = parts[1];
        }

        _httpContext.Request.Headers.Cookie = string.Join("; ", _jar.Select(x => $"{x.Key}={x.Value}"));
        _httpContext.Response.Headers.Remove("Set-Cookie");
    }

    private static DemoStart AStart() => new(
        Guid.NewGuid(),
        "demo-host-1",
        "stamp-1",
        Now.AddHours(3));

    /// <summary>What the browser sends back: the name and value of every cookie the server set.</summary>
    private HttpRequest Echo()
    {
        var pairs = _httpContext.Response.Headers.SetCookie.Select(x => x!.Split(';')[0]);
        var request = new DefaultHttpContext().Request;
        request.Headers.Cookie = string.Join("; ", pairs);

        return request;
    }

    [Fact]
    public async Task Start_SetsTheCookiesOfTheHost_ThatAnAuthenticatedHostRequestReads_WithoutASpotifyToken()
    {
        var start = AStart();
        _service.StartResult = start;

        var result = await _controller.Start(CancellationToken.None);

        var started = Assert.IsType<DemoStartedDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(new DemoStartedDto(start.SessionId, start.ExpiresAt), started);

        var request = Echo();
        Assert.True(_cookies.TryReadProfileId(request, out var profileId));
        Assert.Equal(start.ProfileId, profileId);
        Assert.Null(request.Cookies[CookieNames.Auth]);
    }

    [Fact]
    public async Task Start_UnlocksTheSession_WithoutAPassword()
    {
        var start = AStart();
        _service.StartResult = start;

        await _controller.Start(CancellationToken.None);

        Assert.True(_cookies.TryReadSessionAccess(Echo(), start.SessionId, out var stamp));
        Assert.Equal("stamp-1", stamp);
    }

    [Fact]
    public async Task Start_RemembersTheSessionAsOneThisBrowserStarted()
    {
        var start = AStart();
        _service.StartResult = start;

        await _controller.Start(CancellationToken.None);

        Assert.Equal([new StartedDemoSession(start.SessionId, start.ProfileId)], _cookies.ReadDemoSessions(Echo()));
    }

    [Fact]
    public async Task GetMine_ListsWhatTheBrowsersCookieRemembers()
    {
        var first = AStart();
        var second = AStart() with { ProfileId = "demo-host-2" };
        _service.StartResult = first;
        await _controller.Start(CancellationToken.None);
        Roundtrip();
        _service.StartResult = second;
        await _controller.Start(CancellationToken.None);
        Roundtrip();
        var answer = new MyDemoSessionsDto([new MyDemoSessionDto(second.SessionId, "Demo party", second.ExpiresAt)], Now);
        _service.ListResult = answer;

        var result = await _controller.GetMine(CancellationToken.None);

        Assert.Same(answer, Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(
            [new StartedDemoSession(first.SessionId, "demo-host-1"), new StartedDemoSession(second.SessionId, "demo-host-2")],
            _service.LastStarted);
    }

    [Fact]
    public async Task Resume_ForASessionThisBrowserDidNotStart_IsNotFound_WithoutAskingTheService()
    {
        var result = await _controller.Resume(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Null(_service.LastResumed);
        Assert.Equal(0, _httpContext.Response.Headers.SetCookie.Count);
    }

    [Fact]
    public async Task Resume_MakesTheVisitorTheHostOfThatSessionAgain()
    {
        var older = AStart();
        var newer = AStart() with { ProfileId = "demo-host-2" };
        _service.StartResult = older;
        await _controller.Start(CancellationToken.None);
        Roundtrip();
        _service.StartResult = newer;
        await _controller.Start(CancellationToken.None);
        Roundtrip();
        _service.ResumeResult = older;

        var result = await _controller.Resume(older.SessionId, CancellationToken.None);

        Assert.Equal(new StartedDemoSession(older.SessionId, "demo-host-1"), _service.LastResumed);
        var started = Assert.IsType<DemoStartedDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(older.SessionId, started.SessionId);
        var request = Echo();
        Assert.True(_cookies.TryReadProfileId(request, out var profileId));
        Assert.Equal("demo-host-1", profileId);
        Assert.True(_cookies.TryReadSessionAccess(request, older.SessionId, out var stamp));
        Assert.Equal("stamp-1", stamp);
    }

    [Fact]
    public async Task Resume_OfASessionThatIsGone_AnswersTheError_AndSetsNoCookie()
    {
        var start = AStart();
        _service.StartResult = start;
        await _controller.Start(CancellationToken.None);
        Roundtrip();
        _service.ResumeResult = Error.NotFound(description: "gone");

        var result = await _controller.Resume(start.SessionId, CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Equal(0, _httpContext.Response.Headers.SetCookie.Count);
    }

    [Theory]
    [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict)]
    public async Task Start_WhenRefused_SetsNoCookie_AndAnswersWithTheStatus(ErrorType type, int status)
    {
        _service.StartResult = type == ErrorType.NotFound
            ? Error.NotFound(description: "The demo is not enabled")
            : Error.Conflict(description: "The demo is full right now, try again in a few minutes");

        var result = await _controller.Start(CancellationToken.None);

        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        Assert.Equal(0, _httpContext.Response.Headers.SetCookie.Count);
    }

    [Fact]
    public void GetConfig_AnswersWhatTheServiceSays()
    {
        var config = new DemoConfigDto(true, Now);
        _service.Config = config;

        var result = _controller.GetConfig();

        Assert.Same(config, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task GetSession_AnswersWhenTheSessionEnds()
    {
        var sessionId = Guid.NewGuid();
        var dto = new DemoSessionDto(Now.AddHours(1), Now);
        _service.SessionResult = dto;

        var result = await _controller.GetSession(sessionId, CancellationToken.None);

        Assert.Same(dto, Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(sessionId, _service.LastSessionId);
    }

    [Fact]
    public async Task GetSession_ForASessionThatIsNotADemoOne_IsNotFound()
    {
        _service.SessionResult = Error.NotFound(description: "The demo is not enabled");

        var result = await _controller.GetSession(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public void StartingIsRateLimited_PerIp()
    {
        var method = typeof(DemoController).GetMethod(nameof(DemoController.Start))!;

        var limit = method.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.Equal(DemoController.StartRateLimitPolicy, limit!.PolicyName);
    }

    [Fact]
    public void WhenTheSessionEnds_IsOnlyForThoseWhoUnlockedTheSession()
    {
        var method = typeof(DemoController).GetMethod(nameof(DemoController.GetSession))!;

        Assert.NotNull(method.GetCustomAttribute<RequireSessionAccessAttribute>());
        Assert.Equal("sessions/{sessionId:guid}/demo", method.GetCustomAttributes<HttpMethodAttribute>().Single().Template);
    }

    [Fact]
    public void TheConfigAndTheStart_AreForEverybody()
    {
        foreach (var name in new[] { nameof(DemoController.GetConfig), nameof(DemoController.Start), nameof(DemoController.GetMine), nameof(DemoController.Resume) })
        {
            var method = typeof(DemoController).GetMethod(name)!;

            Assert.Null(method.GetCustomAttribute<RequireSessionAccessAttribute>());
        }
    }

    private sealed class FakeDemoSessionService : IDemoSessionService, IDemoSessionStartService, IDemoMode
    {
        public ErrorOr<DemoStart> StartResult { get; set; }
        public DemoConfigDto Config { get; set; } = new(false, Now);
        public ErrorOr<DemoSessionDto> SessionResult { get; set; }
        public Guid? LastSessionId { get; private set; }
        public ErrorOr<MyDemoSessionsDto> ListResult { get; set; }
        public IReadOnlyCollection<StartedDemoSession> LastStarted { get; private set; } = [];
        public ErrorOr<DemoStart> ResumeResult { get; set; }
        public StartedDemoSession? LastResumed { get; private set; }

        public Task<ErrorOr<MyDemoSessionsDto>> ListStartedAsync(
            IReadOnlyCollection<StartedDemoSession> started,
            CancellationToken cancellationToken)
        {
            LastStarted = started;
            return Task.FromResult(ListResult);
        }

        public Task<ErrorOr<DemoStart>> ResumeAsync(StartedDemoSession started, CancellationToken cancellationToken)
        {
            LastResumed = started;
            return Task.FromResult(ResumeResult);
        }

        public bool IsEnabled => Config.Enabled;

        public DemoConfigDto GetConfig() => Config;

        public Task<ErrorOr<DemoStart>> StartAsync(CancellationToken cancellationToken) => Task.FromResult(StartResult);

        public Task<ErrorOr<DemoSessionDto>> GetAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            LastSessionId = sessionId;
            return Task.FromResult(SessionResult);
        }
    }
}
