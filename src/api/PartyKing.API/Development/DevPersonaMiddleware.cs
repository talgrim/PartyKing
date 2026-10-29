using System.Text.RegularExpressions;
using Microsoft.Extensions.Primitives;
using PartyKing.Infrastructure.Authentication;

namespace PartyKing.API.Development;

/// <summary>
/// Development only. Lets one browser act as several guests: a request with an <c>X-Dev-Persona</c> header (the UI sends
/// it from the tab's <c>?persona=</c>) is a plain guest, because the host and admin cookies are hidden from it, and
/// keeps its own game player cookies, stored under the persona's name. The session unlock cookie stays shared, so the
/// password is entered once per browser. Never registered outside Development.
/// </summary>
public sealed partial class DevPersonaMiddleware
{
    public const string HeaderName = "X-Dev-Persona";

    private static readonly string[] HiddenCookies =
    [
        Constants.UserCookieName,
        Constants.AuthCookieName,
        Constants.AdminCookieName,
    ];

    private static readonly string[] PerPersonaCookies =
    [
        Constants.RingoPlayerCookieName,
        Constants.ResistancePlayerCookieName,
    ];

    private readonly RequestDelegate _next;

    public DevPersonaMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var header)
            || header.ToString() is not { } persona
            || !ValidPersona().IsMatch(persona))
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

    private static string RewriteRequestCookies(HttpRequest request, string persona)
    {
        var cookies = new List<string>();
        foreach (var (name, value) in request.Cookies)
        {
            if (HiddenCookies.Contains(name) || PerPersonaCookies.Contains(name))
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
