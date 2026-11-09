using Microsoft.AspNetCore.Http;
using PartyKing.API.Cookies;
using PartyKing.API.Development;
using PartyKing.API.Hubs;

namespace PartyKing.Api.Tests;

public class DevPersonaMiddlewareTests
{
    private static async Task<string> CookiesSeenByTheNextMiddlewareAsync(string path, string query, string? header = null)
    {
        var seen = "";
        var middleware = new DevPersonaMiddleware(context =>
        {
            seen = context.Request.Headers.Cookie.ToString();
            return Task.CompletedTask;
        });
        var http = new DefaultHttpContext();
        http.Request.Path = path;
        http.Request.QueryString = new QueryString(query);
        http.Request.Headers.Cookie =
            $"{CookieNames.ResistancePlayer}=base; {CookieNames.ResistancePlayer}.anna=anna-player; {CookieNames.User}=host";
        if (header is not null)
        {
            http.Request.Headers[DevPersonaMiddleware.HeaderName] = header;
        }

        await middleware.InvokeAsync(http);

        return seen;
    }

    [Fact]
    public async Task AHubConnectionTakesItsPersonaFromTheQuery()
    {
        var seen = await CookiesSeenByTheNextMiddlewareAsync(SessionHub.Path, "?sessionId=x&persona=anna");

        Assert.Equal($"{CookieNames.ResistancePlayer}=anna-player", seen);
    }

    [Fact]
    public async Task TheQueryIsIgnoredOutsideTheHub()
    {
        var seen = await CookiesSeenByTheNextMiddlewareAsync("/sessions", "?persona=anna");

        Assert.Contains($"{CookieNames.User}=host", seen);
    }

    [Fact]
    public async Task ARequestWithTheHeaderStillWorks()
    {
        var seen = await CookiesSeenByTheNextMiddlewareAsync("/sessions", "", header: "anna");

        Assert.Equal($"{CookieNames.ResistancePlayer}=anna-player", seen);
    }

    [Fact]
    public async Task AnInvalidPersonaChangesNothing()
    {
        var seen = await CookiesSeenByTheNextMiddlewareAsync(SessionHub.Path, "?persona=bad%20name");

        Assert.Contains($"{CookieNames.User}=host", seen);
    }

    [Fact]
    public async Task APersonaDoesNotCarryTheSignedInAccount()
    {
        var seen = "";
        var middleware = new DevPersonaMiddleware(context =>
        {
            seen = context.Request.Headers.Cookie.ToString();
            return Task.CompletedTask;
        });
        var http = new DefaultHttpContext();
        http.Request.Path = "/sessions";
        http.Request.Headers.Cookie = $"{CookieNames.Account}=signed-in";
        http.Request.Headers[DevPersonaMiddleware.HeaderName] = "anna";

        await middleware.InvokeAsync(http);

        Assert.DoesNotContain(CookieNames.Account, seen);
    }
}
