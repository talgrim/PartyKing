using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Ports;
using PartyKing.Application.System;
using PartyKing.Contract.V1.Enums;
using PartyKing.Infrastructure.Metrics;

namespace PartyKing.Infrastructure.Identity;

/// <summary>
/// Asks the identity service <c>GET /api/session[?role=]</c> with the session cookie and nothing else, and remembers
/// the answer for a short time. A request without the session cookie never calls it, so guests cost nothing.
/// </summary>
internal sealed class IdentityClient : IIdentityClient
{
    public const string HttpClientName = "identity";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IdentityCache _cache;
    private readonly IdentityMetrics _metrics;
    private readonly IDateTimeProvider _clock;
    private readonly IOptions<IdentityConfiguration> _options;
    private readonly ILogger<IdentityClient> _logger;

    public IdentityClient(
        IHttpClientFactory httpClientFactory,
        IdentityCache cache,
        IdentityMetrics metrics,
        IDateTimeProvider clock,
        IOptions<IdentityConfiguration> options,
        ILogger<IdentityClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _metrics = metrics;
        _clock = clock;
        _options = options;
        _logger = logger;
    }

    public async Task<IdentityResult> GetCallerAsync(
        string? cookieHeader,
        AccountRole? requiredRole,
        CancellationToken cancellationToken)
    {
        var settings = _options.Value;
        var sessionCookie = FindSessionCookie(cookieHeader, settings.CookieName);

        if (sessionCookie is null)
        {
            return IdentityResult.Anonymous.Instance;
        }

        var key = CacheKey(sessionCookie, requiredRole);
        var now = _clock.UtcNow;

        _cache.TryGet(key, out var cached);

        if (cached is not null && cached.FreshUntil > now)
        {
            _metrics.RecordCacheHit();

            return cached.Result;
        }

        var result = await FetchAsync(sessionCookie, settings.CookieName, requiredRole, cancellationToken);

        if (result is IdentityResult.Unavailable)
        {
            // A cookie that was valid a moment ago stays valid while the identity service is down, so that an outage
            // does not log everybody out. Anything else cannot be answered
            if (cached is { Result: IdentityResult.Account, StaleUntil: var staleUntil } && staleUntil > now)
            {
                _metrics.RecordStaleAnswer();

                return cached.Result;
            }

            return result;
        }

        Remember(key, result, settings, now);

        return result;
    }

    private void Remember(string key, IdentityResult result, IdentityConfiguration settings, DateTimeOffset now)
    {
        var freshFor = TimeSpan.FromSeconds(
            result is IdentityResult.Account ? settings.CacheSeconds : settings.NegativeCacheSeconds);
        var keptFor = result is IdentityResult.Account
            ? TimeSpan.FromSeconds(Math.Max(settings.StaleSeconds, settings.CacheSeconds))
            : freshFor;

        _cache.Set(key, new IdentityCacheEntry(result, now + freshFor, now + keptFor), now + keptFor, now);
    }

    private async Task<IdentityResult> FetchAsync(
        string sessionCookie,
        string cookieName,
        AccountRole? requiredRole,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await SendAsync(sessionCookie, cookieName, requiredRole, cancellationToken);

        _metrics.RecordCall(Outcome(result), stopwatch.Elapsed.TotalMilliseconds);

        return result;
    }

    private async Task<IdentityResult> SendAsync(
        string sessionCookie,
        string cookieName,
        AccountRole? requiredRole,
        CancellationToken cancellationToken)
    {
        var path = requiredRole is null ? "api/session" : $"api/session?role={requiredRole}";

        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        // Only the session cookie goes out: the identity service has no use for the host's own cookies
        request.Headers.TryAddWithoutValidation("Cookie", $"{cookieName}={sessionCookie}");

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);

            using var response = await client.SendAsync(request, cancellationToken);

            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    return await ReadAccountAsync(response, cancellationToken);
                case HttpStatusCode.Unauthorized:
                    return IdentityResult.Anonymous.Instance;
                case HttpStatusCode.Forbidden:
                    return IdentityResult.Forbidden.Instance;
                default:
                    _logger.LogWarning("The identity service answered {StatusCode}", (int)response.StatusCode);

                    return IdentityResult.Unavailable.Instance;
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            // A caller that gave up is not an outage; a timeout is (HttpClient reports it as a cancellation too)
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogWarning(exception, "The identity service could not be reached");

            return IdentityResult.Unavailable.Instance;
        }
    }

    private static async Task<IdentityResult> ReadAccountAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadFromJsonAsync<SessionBody>(JsonOptions, cancellationToken);

        if (body is null || body.Id == Guid.Empty)
        {
            return IdentityResult.Unavailable.Instance;
        }

        return new IdentityResult.Account(body.Id, body.Username, body.Role);
    }

    /// <summary>The value of the session cookie in a <c>Cookie</c> header, or null when it is not there.</summary>
    private static string? FindSessionCookie(string? cookieHeader, string cookieName)
    {
        if (string.IsNullOrEmpty(cookieHeader))
        {
            return null;
        }

        foreach (var part in cookieHeader.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');

            if (separator > 0
                && part.AsSpan(0, separator).Equals(cookieName, StringComparison.Ordinal)
                && separator < part.Length - 1)
            {
                return part[(separator + 1)..];
            }
        }

        return null;
    }

    // The cookie is a credential, so only a hash of it is kept
    private static string CacheKey(string sessionCookie, AccountRole? requiredRole)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sessionCookie)));

        return $"{hash}:{requiredRole?.ToString() ?? "-"}";
    }

    private static string Outcome(IdentityResult result)
    {
        return result switch
        {
            IdentityResult.Account => IdentityMetrics.AccountOutcome,
            IdentityResult.Forbidden => IdentityMetrics.ForbiddenOutcome,
            IdentityResult.Anonymous => IdentityMetrics.AnonymousOutcome,
            _ => IdentityMetrics.UnavailableOutcome
        };
    }

    private sealed record SessionBody(Guid Id, string Username, AccountRole Role);
}
