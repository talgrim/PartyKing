using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using PartyKing.API.Cookies;
using PartyKing.Application.Accounts;
using PartyKing.Application.Games.Services.Base;
using PartyKing.Domain.Entities.Auth;
using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.Api.Tests;

/// <summary>The player cookie of a game: the seat (player and stamp) per session, signed.</summary>
public class SignedCookiesTests
{
    private readonly IDataProtectionProvider _protection = DataProtectionProvider.Create("partyking-tests");
    private readonly Guid _session = Guid.NewGuid();

    private SignedCookies Cookies() => new(_protection);

    /// <summary>What the browser sends back after the server set the cookie (name and value only).</summary>
    private static HttpRequest Echo(HttpContext response, string cookieName)
    {
        var setCookie = response.Response.Headers.SetCookie.ToString();
        var value = setCookie.Split(';')[0][(cookieName.Length + 1)..];
        var request = new DefaultHttpContext().Request;
        request.Headers.Cookie = $"{cookieName}={value}";

        return request;
    }

    private HttpRequest Granted(SignedCookies cookies, Guid session, GameSeat seat, HttpRequest? previous = null)
    {
        var context = new DefaultHttpContext();
        if (previous is not null)
        {
            context.Request.Headers.Cookie = previous.Headers.Cookie;
        }

        cookies.GrantGamePlayer(GameKind.Resistance, session, seat, context);

        return Echo(context, CookieNames.ResistancePlayer);
    }

    [Fact]
    public void ASeatWithoutAStamp_RoundTrips()
    {
        var cookies = Cookies();
        var seat = new GameSeat(Guid.NewGuid(), null);

        var request = Granted(cookies, _session, seat);

        Assert.True(cookies.TryReadGamePlayer(GameKind.Resistance, request, _session, out var read));
        Assert.Equal(seat, read);
    }

    [Fact]
    public void ASeatWithAStamp_RoundTrips()
    {
        var cookies = Cookies();
        var seat = new GameSeat(Guid.NewGuid(), Guid.NewGuid());

        var request = Granted(cookies, _session, seat);

        Assert.True(cookies.TryReadGamePlayer(GameKind.Resistance, request, _session, out var read));
        Assert.Equal(seat, read);
    }

    [Fact]
    public void ACookieWrittenBeforeStampsExisted_StillReadsAsASeatWithoutOne()
    {
        var player = Guid.NewGuid();
        var legacy = _protection.CreateProtector("PartyKing.ResistancePlayerCookie")
            .Protect($"{_session:N}:{player:N}");
        var request = new DefaultHttpContext().Request;
        request.Headers.Cookie = $"{CookieNames.ResistancePlayer}={legacy}";

        Assert.True(Cookies().TryReadGamePlayer(GameKind.Resistance, request, _session, out var read));
        Assert.Equal(new GameSeat(player, null), read);
    }

    [Fact]
    public void ADamagedStamp_MakesTheEntryUnreadable_NotAStamplessSeat()
    {
        var player = Guid.NewGuid();
        var damaged = _protection.CreateProtector("PartyKing.ResistancePlayerCookie")
            .Protect($"{_session:N}:{player:N}:not-a-guid");
        var request = new DefaultHttpContext().Request;
        request.Headers.Cookie = $"{CookieNames.ResistancePlayer}={damaged}";

        Assert.False(Cookies().TryReadGamePlayer(GameKind.Resistance, request, _session, out _));
    }

    [Fact]
    public void GrantingAgain_ReplacesTheSeatOfThatSessionAndKeepsTheOthers()
    {
        var cookies = Cookies();
        var otherSession = Guid.NewGuid();
        var other = new GameSeat(Guid.NewGuid(), null);
        var first = new GameSeat(Guid.NewGuid(), null);
        var taken = new GameSeat(first.PlayerId, Guid.NewGuid());

        var request = Granted(cookies, otherSession, other);
        request = Granted(cookies, _session, first, request);
        request = Granted(cookies, _session, taken, request);

        Assert.True(cookies.TryReadGamePlayer(GameKind.Resistance, request, _session, out var read));
        Assert.Equal(taken, read);
        Assert.True(cookies.TryReadGamePlayer(GameKind.Resistance, request, otherSession, out var kept));
        Assert.Equal(other, kept);
    }

    [Fact]
    public void ACookieForAnotherSession_IsNotASeatHere()
    {
        var cookies = Cookies();
        var request = Granted(cookies, Guid.NewGuid(), new GameSeat(Guid.NewGuid(), null));

        Assert.False(cookies.TryReadGamePlayer(GameKind.Resistance, request, _session, out _));
    }

    [Fact]
    public void ATamperedCookie_IsNotASeat()
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.Cookie = $"{CookieNames.ResistancePlayer}=made-up";

        Assert.False(Cookies().TryReadGamePlayer(GameKind.Resistance, request, _session, out _));
    }

    [Fact]
    public void TheCookieOfOneGame_IsNotReadAsTheOthers()
    {
        var cookies = Cookies();
        var request = Granted(cookies, _session, new GameSeat(Guid.NewGuid(), null));
        var asRingo = new DefaultHttpContext().Request;
        asRingo.Headers.Cookie = $"{CookieNames.RingoPlayer}={request.Cookies[CookieNames.ResistancePlayer]}";

        Assert.False(cookies.TryReadGamePlayer(GameKind.Ringo, asRingo, _session, out _));
    }

    private static HttpRequest GrantedAccount(SignedCookies cookies, AccountIdentity identity, out string setCookie)
    {
        var context = new DefaultHttpContext();
        cookies.GrantAccount(identity, context);
        setCookie = context.Response.Headers.SetCookie.ToString();

        return Echo(context, CookieNames.Account);
    }

    [Fact]
    public void AnAccountCookie_RoundTripsTheIdAndTheStamp()
    {
        var cookies = Cookies();
        var identity = new AccountIdentity(Guid.NewGuid(), AccountRole.User, Guid.NewGuid());

        var request = GrantedAccount(cookies, identity, out _);

        Assert.True(cookies.TryReadAccount(request, out var id, out var stamp, out var role));
        Assert.Equal(identity.Id, id);
        Assert.Equal(identity.SecurityStamp, stamp);
        Assert.Equal(AccountRole.User, role);
    }

    [Fact]
    public void AnAdminsAccountCookie_CarriesTheAdminRole()
    {
        var cookies = Cookies();

        var request = GrantedAccount(cookies, new AccountIdentity(Guid.NewGuid(), AccountRole.Admin, Guid.NewGuid()), out _);

        Assert.True(cookies.TryReadAccount(request, out _, out _, out var role));
        Assert.Equal(AccountRole.Admin, role);
    }

    [Fact]
    public void AnAccountCookie_IsHttpOnly_AndLastsLongerForAUserThanForAnAdmin()
    {
        var cookies = Cookies();

        GrantedAccount(cookies, new AccountIdentity(Guid.NewGuid(), AccountRole.User, Guid.NewGuid()), out var user);
        GrantedAccount(cookies, new AccountIdentity(Guid.NewGuid(), AccountRole.Admin, Guid.NewGuid()), out var admin);

        Assert.Contains("httponly", user, StringComparison.OrdinalIgnoreCase);
        Assert.InRange(ExpiresIn(user), TimeSpan.FromDays(29), TimeSpan.FromDays(31));
        Assert.InRange(ExpiresIn(admin), TimeSpan.FromHours(11), TimeSpan.FromHours(13));
    }

    private static TimeSpan ExpiresIn(string setCookie)
    {
        var expires = setCookie.Split(';')
            .Select(x => x.Trim())
            .Single(x => x.StartsWith("expires=", StringComparison.OrdinalIgnoreCase))["expires=".Length..];

        return DateTimeOffset.Parse(expires) - DateTimeOffset.UtcNow;
    }

    [Fact]
    public void ATamperedAccountCookie_IsNobody()
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.Cookie = $"{CookieNames.Account}=made-up";

        Assert.False(Cookies().TryReadAccount(request, out _, out _, out _));
    }

    [Fact]
    public void AMissingAccountCookie_IsNobody()
    {
        Assert.False(Cookies().TryReadAccount(new DefaultHttpContext().Request, out _, out _, out _));
    }

    [Fact]
    public void TheHostCookie_IsNotReadAsAnAccountCookie()
    {
        var cookies = Cookies();
        var context = new DefaultHttpContext();
        cookies.GrantHost("spotify-profile", context);
        var host = Echo(context, CookieNames.User);
        var asAccount = new DefaultHttpContext().Request;
        asAccount.Headers.Cookie = $"{CookieNames.Account}={host.Cookies[CookieNames.User]}";

        Assert.False(cookies.TryReadAccount(asAccount, out _, out _, out _));
    }

    [Fact]
    public void RevokingTheAccountCookie_ExpiresIt()
    {
        var context = new DefaultHttpContext();

        Cookies().RevokeAccount(context);

        var setCookie = context.Response.Headers.SetCookie.ToString();
        Assert.StartsWith($"{CookieNames.Account}=;", setCookie);
    }
}
