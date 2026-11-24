using Microsoft.Extensions.Options;
using PartyKing.API.Cookies;
using PartyKing.API.Hubs;
using PartyKing.Application.Configuration;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Primitives;

namespace PartyKing.API.Development;

/// <summary>
/// Development only. Lets one browser act as several guests: a request with an <c>X-Dev-Persona</c> header (the UI sends
/// it from the tab's <c>?persona=</c>) is a plain guest, because the host and admin cookies are hidden from it, and
/// keeps its own game player cookies, stored under the persona's name. The session unlock cookie stays shared, so the
/// password is entered once per browser. A hub connection cannot set headers, so it sends the persona as the
/// <c>persona</c> query value instead. Never registered outside Development.
/// </summary>
public sealed partial class DevPersonaMiddleware
{
    public const string HeaderName = "X-Dev-Persona";
    public const string QueryName = "persona";

    private static readonly string[] PerPersonaCookies =
    [
        CookieNames.RingoPlayer,
        CookieNames.ResistancePlayer,
    ];

    private readonly RequestDelegate _next;

    // The host's own cookies and the identity service's session cookie: a persona is a plain guest
    private readonly string[] _hiddenCookies;

    public DevPersonaMiddleware(RequestDelegate next, IOptions<IdentityConfiguration> identityOptions)
    {
        _next = next;
        _hiddenCookies = [CookieNames.User, CookieNames.Auth, identityOptions.Value.CookieName];
    }

    public Task InvokeAsync(HttpContext context)
    {
        var persona = ReadPersona(context.Request);
        if (persona is null)
        {
            return _next(context);
        }

        context.Request.Headers.Cookie = RewriteRequestCookies(context.Request, persona);
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.SetCookie = RewriteSetCookies(context.Response.Headers.SetCookie, persona);

            return Task.CompletedTask;
        });

        return _next(context);
    }

    private static string? ReadPersona(HttpRequest request)
    {
        var persona = request.Headers.TryGetValue(HeaderName, out var header)
            ? header.ToString()
            : request.Path.StartsWithSegments(SessionHub.Path)
                ? request.Query[QueryName].ToString()
                : null;

        return persona is not null && ValidPersona().IsMatch(persona) ? persona : null;
    }

    private string RewriteRequestCookies(HttpRequest request, string persona)
    {
        var cookies = new List<string>();
        foreach (var (name, value) in request.Cookies)
        {
            if (_hiddenCookies.Contains(name) || PerPersonaCookies.Contains(name))
            {
                continue;
            }

            var baseName = PerPersonaCookies.FirstOrDefault(x => name == PersonaCookieName(x, persona));
            cookies.Add($"{baseName ?? name}={value}");
        }

        return string.Join("; ", cookies);
    }

    private static StringValues RewriteSetCookies(StringValues setCookies, string persona)
    {
        return setCookies
            .Select(cookie => PerPersonaCookies.FirstOrDefault(x => cookie!.StartsWith(x + "=", StringComparison.Ordinal))
                is { } name
                ? PersonaCookieName(name, persona) + cookie![name.Length..]
                : cookie!)
            .ToArray();
    }

    private static string PersonaCookieName(string cookieName, string persona) => $"{cookieName}.{persona}";

    [GeneratedRegex("^[a-zA-Z0-9-]{1,20}$")]
    private static partial Regex ValidPersona();
}
