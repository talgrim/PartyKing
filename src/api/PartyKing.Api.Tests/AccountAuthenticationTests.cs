using Identity.Client;
using Identity.Contract.V1.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.API;
using PartyKing.API.Authentication;
using PartyKing.API.Cookies;
using PartyKing.API.Metrics;

namespace PartyKing.Api.Tests;

/// <summary>
/// The real authentication wiring (the account scheme, the policies) with a fake identity client: the session cookie
/// signs an account in only while the identity service knows it, and only an account that is an admin now counts as one.
/// A signed-in user must never be an admin, in the policy or when a session asks who the caller is, and a request
/// without the session cookie must never cost a call.
/// </summary>
public class AccountAuthenticationTests : IDisposable
{
    private static readonly string CookieName = new IdentityClientOptions().CookieName;

    private readonly FakeIdentityClient _identity = new();
    private readonly ServiceProvider _provider;
    private readonly ISignedCookies _cookies;
    private readonly List<IServiceScope> _scopes = [];

    public AccountAuthenticationTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWebEncoders();
        services.AddOptions<IdentityClientOptions>();
        services.AddSingleton<IDataProtectionProvider>(DataProtectionProvider.Create("partyking-tests"));
        services.AddSingleton<AuthorizationMetrics>();
        services.AddSingleton<IIdentityClient>(_identity);
        DependencyInjection.AddAuthorization(services);

        _provider = services.BuildServiceProvider();
        _cookies = _provider.GetRequiredService<ISignedCookies>();
    }

    public void Dispose()
    {
        _scopes.ForEach(x => x.Dispose());
        _provider.Dispose();
    }

    /// <summary>
    /// A request that carries the given cookie header (none when it is null). Each request has its own DI scope, as in
    /// the server: ASP.NET keeps the authentication handler of a scheme per scope.
    /// </summary>
    private HttpContext RequestWith(string? cookieHeader)
    {
        var scope = _provider.CreateScope();
        _scopes.Add(scope);
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };

        if (cookieHeader is not null)
        {
            http.Request.Headers.Cookie = cookieHeader;
        }

        return http;
    }

    private async Task<PolicyAuthorizationResult> EvaluateAsync(string policyName, HttpContext http)
    {
        var provider = http.RequestServices.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = (await provider.GetPolicyAsync(policyName))!;
        var evaluator = http.RequestServices.GetRequiredService<IPolicyEvaluator>();
        var authentication = await evaluator.AuthenticateAsync(policy, http);

        return await evaluator.AuthorizeAsync(policy, authentication, http, null);
    }

    /// <summary>An account the identity service knows, and the request of a browser that has its session cookie.</summary>
    private HttpContext SignedUp(AccountRole role, out IdentityResult.Account account)
    {
        var token = Guid.NewGuid().ToString("N");
        account = new IdentityResult.Account(Guid.NewGuid(), "ann", role);
        _identity.Known[token] = account;

        return RequestWith($"{CookieNames.User}=host; {CookieName}={token}");
    }

    [Fact]
    public async Task AUser_PassesTheAccountPolicy_ButNotTheAdminOne()
    {
        var asAccount = await EvaluateAsync(Policies.Account, SignedUp(AccountRole.User, out _));
        var asAdmin = await EvaluateAsync(Policies.Admin, SignedUp(AccountRole.User, out _));

        Assert.True(asAccount.Succeeded);
        Assert.False(asAdmin.Succeeded);
        Assert.True(asAdmin.Forbidden);
    }

    [Fact]
    public async Task AnAdmin_PassesBothPolicies()
    {
        Assert.True((await EvaluateAsync(Policies.Account, SignedUp(AccountRole.Admin, out _))).Succeeded);
        Assert.True((await EvaluateAsync(Policies.Admin, SignedUp(AccountRole.Admin, out _))).Succeeded);
    }

    [Fact]
    public async Task TheAccountTheIdentityServiceNamed_IsTheOneTheRequestActsAs()
    {
        var http = SignedUp(AccountRole.User, out var account);

        var result = await http.AuthenticateAsync(SchemeNames.Account);

        Assert.Equal(account.Id.ToString(), result.Principal!.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        Assert.Equal("ann", result.Principal.Identity!.Name);
    }

    [Fact]
    public async Task AUserIsNotAnAdminToASession_ButAnAdminIs()
    {
        var asUser = await SignedUp(AccountRole.User, out _).ReadSessionCallerAsync(_cookies);
        var asAdmin = await SignedUp(AccountRole.Admin, out _).ReadSessionCallerAsync(_cookies);
        var anonymous = await RequestWith(null).ReadSessionCallerAsync(_cookies);

        Assert.False(asUser.IsAdmin);
        Assert.True(asAdmin.IsAdmin);
        Assert.False(anonymous.IsAdmin);
    }

    /// <summary>
    /// The polled session endpoints (every game fetch) ask who the caller is, and guests have no account: they must
    /// not cost a call to the identity service.
    /// </summary>
    [Fact]
    public async Task NoSessionCookie_CostsNoCall_WhenASessionAsksWhoTheCallerIs()
    {
        await RequestWith(null).ReadSessionCallerAsync(_cookies);
        await RequestWith($"{CookieNames.User}=host; {CookieNames.Auth}=token").ReadSessionCallerAsync(_cookies);

        Assert.Equal(0, _identity.Calls);
    }

    [Fact]
    public async Task NoSessionCookie_IsNoResult_NotAFailure()
    {
        var result = await RequestWith($"{CookieNames.User}=host").AuthenticateAsync(SchemeNames.Account);

        Assert.True(result.None);
        Assert.Equal(0, _identity.Calls);
    }

    [Fact]
    public async Task TheSessionCookieAskedAbout_IsTheOneTheBrowserSent()
    {
        var http = SignedUp(AccountRole.User, out _);

        await http.AuthenticateAsync(SchemeNames.Account);

        Assert.Contains($"{CookieName}=", _identity.LastCookieHeader);
    }

    [Fact]
    public async Task TheIdentityServiceIsAskedOnce_PerRequest()
    {
        var http = SignedUp(AccountRole.Admin, out _);

        var first = await http.ReadSessionCallerAsync(_cookies);
        var second = await http.ReadSessionCallerAsync(_cookies);
        var policy = await EvaluateAsync(Policies.Admin, http);

        Assert.True(first.IsAdmin);
        Assert.True(second.IsAdmin);
        Assert.True(policy.Succeeded);
        Assert.Equal(1, _identity.Calls);
    }

    [Fact]
    public async Task TheRoleIsTheOneTheIdentityServiceSaysNow()
    {
        var http = SignedUp(AccountRole.Admin, out var account);
        _identity.Known[_identity.Known.Keys.Single()] = account with { Role = AccountRole.User };

        var caller = await http.ReadSessionCallerAsync(_cookies);
        var policy = await EvaluateAsync(Policies.Admin, http);

        Assert.False(caller.IsAdmin);
        Assert.False(policy.Succeeded);
    }

    [Fact]
    public async Task ACookieTheIdentityServiceDoesNotKnow_SignsNobodyIn_AndIsChallengedAs401()
    {
        var http = RequestWith($"{CookieName}=expired");

        var caller = await http.ReadSessionCallerAsync(_cookies);
        var policy = await EvaluateAsync(Policies.Account, http);
        await http.ChallengeAsync(SchemeNames.Account);

        Assert.False(caller.IsAdmin);
        Assert.False(policy.Succeeded);
        Assert.True(policy.Challenged);
        Assert.Equal(StatusCodes.Status401Unauthorized, http.Response.StatusCode);
    }

    [Fact]
    public async Task NoCookie_IsChallenged()
    {
        var policy = await EvaluateAsync(Policies.Admin, RequestWith(null));

        Assert.True(policy.Challenged);
    }

    /// <summary>The UI reads a 401 as "signed out" and drops its hint, so an outage must not look like one.</summary>
    [Fact]
    public async Task WhenTheIdentityServiceIsDown_TheChallengeIs503_Not401()
    {
        var http = SignedUp(AccountRole.User, out _);
        _identity.Down = true;

        var policy = await EvaluateAsync(Policies.Account, http);
        await http.ChallengeAsync(SchemeNames.Account);

        Assert.False(policy.Succeeded);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, http.Response.StatusCode);
    }

    [Fact]
    public async Task WhenTheIdentityServiceIsDown_ASessionSeesAPlainGuest_NotAFailedRequest()
    {
        var http = SignedUp(AccountRole.Admin, out _);
        _identity.Down = true;

        var caller = await http.ReadSessionCallerAsync(_cookies);

        Assert.False(caller.IsAdmin);
    }

    private sealed class FakeIdentityClient : IIdentityClient
    {
        /// <summary>The accounts by the value of their session cookie.</summary>
        public Dictionary<string, IdentityResult.Account> Known { get; } = [];

        public bool Down { get; set; }

        public int Calls { get; private set; }

        public string? LastCookieHeader { get; private set; }

        public Task<IdentityResult> GetCallerAsync(
            string? cookieHeader,
            AccountRole? requiredRole,
            CancellationToken cancellationToken)
        {
            Calls++;
            LastCookieHeader = cookieHeader;

            if (Down)
            {
                return Task.FromResult<IdentityResult>(IdentityResult.Unavailable.Instance);
            }

            var token = cookieHeader?
                .Split(';', StringSplitOptions.TrimEntries)
                .Where(x => x.StartsWith($"{CookieName}=", StringComparison.Ordinal))
                .Select(x => x[(CookieName.Length + 1)..])
                .FirstOrDefault();

            if (token is null || !Known.TryGetValue(token, out var account))
            {
                return Task.FromResult<IdentityResult>(IdentityResult.Anonymous.Instance);
            }

            return Task.FromResult<IdentityResult>(
                requiredRole is not null && account.Role != requiredRole
                    ? IdentityResult.Forbidden.Instance
                    : account);
        }
    }
}
