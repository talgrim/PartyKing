using System.Diagnostics.Metrics;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Ports;
using PartyKing.Contract.V1.Enums;
using PartyKing.Infrastructure.Identity;
using PartyKing.Infrastructure.Metrics;

namespace PartyKing.Application.Tests;

/// <summary>
/// The client of the identity service: what it asks, what it remembers and for how long. The answers are the identity
/// service's (<c>200 {id, username, role}</c>, 401, 403); anything else says nothing about the caller.
/// </summary>
public class IdentityClientTests : IDisposable
{
    private const string Cookie = "__Secure-talgrim.session";
    private static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly FakeClock _clock = new();
    private readonly IdentityConfiguration _settings = new();
    private readonly IdentityCache _cache = new();
    private readonly IdentityMetrics _metrics = new();
    private readonly FakeIdentityHandler _handler = new();
    private readonly IdentityClient _client;

    public IdentityClientTests()
    {
        _client = new IdentityClient(
            new FakeHttpClientFactory(_handler),
            _cache,
            _metrics,
            _clock,
            Options.Create(_settings),
            NullLogger<IdentityClient>.Instance);
    }

    public void Dispose()
    {
        _cache.Dispose();
        _metrics.Dispose();
        _handler.Dispose();
    }

    private Task<IdentityResult> AskAsync(string? cookieHeader = $"{Cookie}=abc", AccountRole? role = null) =>
        _client.GetCallerAsync(cookieHeader, role, CancellationToken.None);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("cookie.partyKing.user=host")]
    [InlineData($"{Cookie}=")]
    public async Task ARequestWithoutTheSessionCookie_IsAnonymous_AndCostsNoCall(string? cookieHeader)
    {
        var result = await AskAsync(cookieHeader);

        Assert.IsType<IdentityResult.Anonymous>(result);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task ACookieTheServiceKnows_IsTheAccountItNames()
    {
        _handler.Respond(HttpStatusCode.OK, $$"""{"id":"{{AccountId}}","username":"ann","role":"Admin"}""");

        var result = await AskAsync();

        Assert.Equal(new IdentityResult.Account(AccountId, "ann", AccountRole.Admin), result);
    }

    [Fact]
    public async Task OnlyTheSessionCookie_IsForwarded_ToTheSessionEndpoint()
    {
        _handler.Respond(HttpStatusCode.OK, $$"""{"id":"{{AccountId}}","username":"ann","role":"User"}""");

        await AskAsync($"cookie.partyKing.user=host; {Cookie}=abc; cookie.partyKing.auth=token");

        var request = Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/session", request.Path);
        Assert.Equal($"{Cookie}=abc", request.CookieHeader);
    }

    [Fact]
    public async Task ARequiredRole_IsAskedAsAQueryValue()
    {
        _handler.Respond(HttpStatusCode.Forbidden);

        var result = await AskAsync(role: AccountRole.Admin);

        Assert.IsType<IdentityResult.Forbidden>(result);
        Assert.Equal("/api/session?role=Admin", Assert.Single(_handler.Requests).PathAndQuery);
    }

    [Fact]
    public async Task A401_IsAnonymous()
    {
        _handler.Respond(HttpStatusCode.Unauthorized);

        Assert.IsType<IdentityResult.Anonymous>(await AskAsync());
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task AnErrorFromTheService_SaysNothingAboutTheCaller(HttpStatusCode status)
    {
        _handler.Respond(status);

        Assert.IsType<IdentityResult.Unavailable>(await AskAsync());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("null")]
    public async Task AnAnswerThatIsNotAnAccount_SaysNothingAboutTheCaller(string body)
    {
        _handler.Respond(HttpStatusCode.OK, body);

        Assert.IsType<IdentityResult.Unavailable>(await AskAsync());
    }

    [Fact]
    public async Task AServiceThatCannotBeReached_IsUnavailable()
    {
        _handler.Fail(new HttpRequestException("connection refused"));

        Assert.IsType<IdentityResult.Unavailable>(await AskAsync());
    }

    [Fact]
    public async Task ATimeout_IsUnavailable()
    {
        _handler.Fail(new TaskCanceledException("timed out"));

        Assert.IsType<IdentityResult.Unavailable>(await AskAsync());
    }

    [Fact]
    public async Task ACallerWhoGaveUp_IsNotAnOutage()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        _handler.Fail(new TaskCanceledException("cancelled"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _client.GetCallerAsync($"{Cookie}=abc", null, cancelled.Token));
    }

    [Fact]
    public async Task AnAnswer_IsUsedWithoutAskingAgain_UntilItIsOld()
    {
        _handler.RespondWithAccount(AccountId);

        var first = await AskAsync();
        _clock.Advance(TimeSpan.FromSeconds(_settings.CacheSeconds - 1));
        var second = await AskAsync();

        Assert.Equal(first, second);
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task AnAnswerThatIsOld_IsAskedAgain()
    {
        _handler.RespondWithAccount(AccountId);

        await AskAsync();
        _clock.Advance(TimeSpan.FromSeconds(_settings.CacheSeconds + 1));
        await AskAsync();

        Assert.Equal(2, _handler.Requests.Count);
    }

    [Fact]
    public async Task ARejectedCookie_IsRememberedBriefly_SoGuessingItCostsOneCall()
    {
        _handler.Respond(HttpStatusCode.Unauthorized);

        await AskAsync();
        await AskAsync();
        Assert.Single(_handler.Requests);

        _clock.Advance(TimeSpan.FromSeconds(_settings.NegativeCacheSeconds + 1));
        await AskAsync();

        Assert.Equal(2, _handler.Requests.Count);
    }

    [Fact]
    public async Task ASignedOutBrowser_IsAnonymousAgain_AfterTheShortNegativeEntry()
    {
        _handler.RespondWithAccount(AccountId);
        Assert.IsType<IdentityResult.Account>(await AskAsync());

        _clock.Advance(TimeSpan.FromSeconds(_settings.CacheSeconds + 1));
        _handler.Respond(HttpStatusCode.Unauthorized);

        Assert.IsType<IdentityResult.Anonymous>(await AskAsync());
    }

    [Fact]
    public async Task EveryCookie_HasItsOwnAnswer()
    {
        _handler.RespondWithAccount(AccountId);

        await AskAsync($"{Cookie}=one");
        await AskAsync($"{Cookie}=two");

        Assert.Equal(2, _handler.Requests.Count);
    }

    [Fact]
    public async Task EveryRequiredRole_HasItsOwnAnswer()
    {
        _handler.RespondWithAccount(AccountId);

        await AskAsync(role: null);
        await AskAsync(role: AccountRole.Admin);

        Assert.Equal(2, _handler.Requests.Count);
    }

    [Fact]
    public async Task AnUnavailableService_IsNotRemembered()
    {
        _handler.Respond(HttpStatusCode.InternalServerError);
        await AskAsync();

        _handler.RespondWithAccount(AccountId);
        var result = await AskAsync();

        Assert.IsType<IdentityResult.Account>(result);
    }

    [Fact]
    public async Task WhenTheServiceIsDown_ACookieThatWasValidAMomentAgo_StillIs()
    {
        _handler.RespondWithAccount(AccountId);
        var before = await AskAsync();
        _clock.Advance(TimeSpan.FromSeconds(_settings.CacheSeconds + 1));
        _handler.Fail(new HttpRequestException("down"));

        var during = await AskAsync();

        Assert.Equal(before, during);
    }

    [Fact]
    public async Task WhenTheServiceStaysDown_ThePastAnswerIsNotKeptForever()
    {
        _handler.RespondWithAccount(AccountId);
        await AskAsync();
        _clock.Advance(TimeSpan.FromSeconds(_settings.StaleSeconds + 1));
        _handler.Fail(new HttpRequestException("down"));

        Assert.IsType<IdentityResult.Unavailable>(await AskAsync());
    }

    [Fact]
    public async Task WhenTheServiceIsDown_ACookieItNeverAnswered_IsUnavailable()
    {
        _handler.Fail(new HttpRequestException("down"));

        Assert.IsType<IdentityResult.Unavailable>(await AskAsync());
    }

    [Fact]
    public async Task ARejectedCookie_IsNotKeptAsAnAccount_WhenTheServiceGoesDown()
    {
        _handler.Respond(HttpStatusCode.Unauthorized);
        await AskAsync();
        _clock.Advance(TimeSpan.FromSeconds(_settings.NegativeCacheSeconds + 1));
        _handler.Fail(new HttpRequestException("down"));

        Assert.IsType<IdentityResult.Unavailable>(await AskAsync());
    }

    [Fact]
    public async Task TheMetrics_CountCacheHitsCallsAndStaleAnswers()
    {
        var measurements = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == IdentityMetrics.MeterName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, _, _) => measurements.Add(instrument.Name));
        listener.Start();

        _handler.RespondWithAccount(AccountId);
        await AskAsync();
        await AskAsync();
        _clock.Advance(TimeSpan.FromSeconds(_settings.CacheSeconds + 1));
        _handler.Fail(new HttpRequestException("down"));
        await AskAsync();

        Assert.Equal(1, measurements.Count(x => x == IdentityMetrics.CacheHitsInstrumentName));
        Assert.Equal(2, measurements.Count(x => x == IdentityMetrics.CallsInstrumentName));
        Assert.Equal(1, measurements.Count(x => x == IdentityMetrics.StaleAnswersInstrumentName));
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public FakeHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name) =>
            new(_handler, disposeHandler: false) { BaseAddress = new Uri("https://auth.test/") };
    }

    private sealed record SeenRequest(HttpMethod Method, string Path, string PathAndQuery, string? CookieHeader);

    private sealed class FakeIdentityHandler : HttpMessageHandler
    {
        private Func<HttpResponseMessage> _response = () => new HttpResponseMessage(HttpStatusCode.Unauthorized);

        public List<SeenRequest> Requests { get; } = [];

        public void Respond(HttpStatusCode status, string? body = null) =>
            _response = () => new HttpResponseMessage(status)
            {
                Content = body is null ? null : new StringContent(body, Encoding.UTF8, "application/json")
            };

        public void RespondWithAccount(Guid id) =>
            Respond(HttpStatusCode.OK, $$"""{"id":"{{id}}","username":"ann","role":"User"}""");

        public void Fail(Exception exception) => _response = () => throw exception;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new SeenRequest(
                request.Method,
                request.RequestUri!.AbsolutePath,
                request.RequestUri.PathAndQuery,
                request.Headers.TryGetValues("Cookie", out var values) ? string.Join("; ", values) : null));

            return Task.FromResult(_response());
        }
    }
}
