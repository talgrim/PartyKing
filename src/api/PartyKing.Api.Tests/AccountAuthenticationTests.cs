using System.Text.Encodings.Web;
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
using PartyKing.Application.Accounts;
using PartyKing.Application.Accounts.Services;
using PartyKing.Contract.V1.Accounts;
using PartyKing.Domain.Entities.Auth;
using ErrorOr;

namespace PartyKing.Api.Tests;

/// <summary>
/// The real authentication wiring (the account scheme, the policies) with a fake account service: a cookie signs an
/// account in only while its stamp matches, and only an account that is an admin now counts as one. A signed-in user
/// must never be an admin, in the policy or when a session asks who the caller is.
/// </summary>
public class AccountAuthenticationTests : IDisposable
{
    private readonly FakeAccountService _accounts = new();
    private readonly ServiceProvider _provider;
    private readonly SignedCookies _cookies;
    private readonly List<IServiceScope> _scopes = [];

    public AccountAuthenticationTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWebEncoders();
        services.AddSingleton<IDataProtectionProvider>(DataProtectionProvider.Create("partyking-tests"));
        services.AddSingleton<AuthorizationMetrics>();
        services.AddSingleton<IAccountService>(_accounts);
        DependencyInjection.AddAuthorization(services);

        _provider = services.BuildServiceProvider();
        _cookies = (SignedCookies)_provider.GetRequiredService<ISignedCookies>();
    }

    public void Dispose()
    {
        _scopes.ForEach(x => x.Dispose());
        _provider.Dispose();
    }

    /// <summary>
    /// A request that carries the account cookie the server would set for this identity (and none when it is null).
    /// Each request has its own DI scope, as in the server: ASP.NET keeps the authentication handler of a scheme per scope.
    /// </summary>
    private HttpContext RequestWith(AccountIdentity? cookieOf)
    {
        var scope = _provider.CreateScope();
        _scopes.Add(scope);
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };

        if (cookieOf is not null)
        {
            var response = new DefaultHttpContext();
            _cookies.GrantAccount(cookieOf, response);
            var setCookie = response.Response.Headers.SetCookie.ToString().Split(';')[0];
            http.Request.Headers.Cookie = setCookie;
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

    private AccountIdentity SignedUp(AccountRole role)
    {
        var identity = new AccountIdentity(Guid.NewGuid(), role, Guid.NewGuid());
        _accounts.Known.Add(identity);

        return identity;
    }

    [Fact]
    public async Task AUser_PassesTheAccountPolicy_ButNotTheAdminOne()
    {
        var user = SignedUp(AccountRole.User);

        var asAccount = await EvaluateAsync(Policies.Account, RequestWith(user));
        var asAdmin = await EvaluateAsync(Policies.Admin, RequestWith(user));

        Assert.True(asAccount.Succeeded);
        Assert.False(asAdmin.Succeeded);
        Assert.True(asAdmin.Forbidden);
    }

    [Fact]
    public async Task AnAdmin_PassesBothPolicies()
    {
        var admin = SignedUp(AccountRole.Admin);

        Assert.True((await EvaluateAsync(Policies.Account, RequestWith(admin))).Succeeded);
        Assert.True((await EvaluateAsync(Policies.Admin, RequestWith(admin))).Succeeded);
    }

    [Fact]
    public async Task AUserIsNotAnAdminToASession_ButAnAdminIs()
    {
        var user = SignedUp(AccountRole.User);
        var admin = SignedUp(AccountRole.Admin);

        var asUser = await RequestWith(user).ReadSessionCallerAsync(_cookies);
        var asAdmin = await RequestWith(admin).ReadSessionCallerAsync(_cookies);
        var anonymous = await RequestWith(null).ReadSessionCallerAsync(_cookies);

        Assert.False(asUser.IsAdmin);
        Assert.True(asAdmin.IsAdmin);
        Assert.False(anonymous.IsAdmin);
    }

    [Fact]
    public async Task TheRoleIsTheOneTheAccountHasNow_NotTheOneInTheCookie()
    {
        var admin = SignedUp(AccountRole.Admin);
        var cookieOfAnAdmin = RequestWith(admin);
        _accounts.Known.Remove(admin);
        _accounts.Known.Add(admin with { Role = AccountRole.User });

        var caller = await cookieOfAnAdmin.ReadSessionCallerAsync(_cookies);
        var policy = await EvaluateAsync(Policies.Admin, cookieOfAnAdmin);

        Assert.False(caller.IsAdmin);
        Assert.False(policy.Succeeded);
    }

    [Fact]
    public async Task ACookieWithAnOldStamp_SignsNobodyIn()
    {
        var admin = SignedUp(AccountRole.Admin);
        var cookieOfTheOldStamp = RequestWith(admin);
        _accounts.Known.Remove(admin);
        _accounts.Known.Add(admin with { SecurityStamp = Guid.NewGuid() });

        var caller = await cookieOfTheOldStamp.ReadSessionCallerAsync(_cookies);
        var policy = await EvaluateAsync(Policies.Account, cookieOfTheOldStamp);

        Assert.False(caller.IsAdmin);
        Assert.False(policy.Succeeded);
        Assert.True(policy.Challenged);
    }

    [Fact]
    public async Task ADeletedAccount_SignsNobodyIn()
    {
        var user = SignedUp(AccountRole.User);
        var http = RequestWith(user);
        _accounts.Known.Clear();

        var policy = await EvaluateAsync(Policies.Account, http);

        Assert.False(policy.Succeeded);
        Assert.True(policy.Challenged);
    }

    [Fact]
    public async Task NoCookie_IsChallenged()
    {
        var policy = await EvaluateAsync(Policies.Admin, RequestWith(null));

        Assert.True(policy.Challenged);
    }

    private sealed class FakeAccountService : IAccountService
    {
        public List<AccountIdentity> Known { get; } = [];

        public Task<AccountIdentity?> ValidateAsync(Guid accountId, Guid securityStamp, CancellationToken cancellationToken) =>
            Task.FromResult(Known.FirstOrDefault(x => x.Id == accountId && x.SecurityStamp == securityStamp));

        public Task<ErrorOr<AccountIdentity>> RegisterAsync(string? username, string? password, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ErrorOr<AccountIdentity>> LoginAsync(string? username, string? password, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ErrorOr<AccountDto>> GetAsync(Guid accountId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ErrorOr<AccountIdentity>> ChangePasswordAsync(Guid accountId, string? currentPassword, string? newPassword, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ErrorOr<Success>> SignOutEverywhereAsync(Guid accountId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ErrorOr<Success>> DeleteAsync(Guid accountId, string? password, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
