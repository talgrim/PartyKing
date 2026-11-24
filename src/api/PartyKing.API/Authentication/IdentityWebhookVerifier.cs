using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.System;

namespace PartyKing.API.Authentication;

public interface IIdentityWebhookVerifier
{
    /// <summary>True when the request, with the exact bytes of its body, was signed by the identity service a moment ago.</summary>
    bool IsSigned(HttpRequest request, byte[] body);
}

/// <summary>
/// Checks the signature of a call from the identity service: <c>X-Identity-Signature: sha256=&lt;hex&gt;</c>, the
/// HMAC-SHA256 of <c>"{X-Identity-Timestamp}.{body}"</c> under the shared secret, over the exact bytes of the body, in
/// lower-case hex. The path is not signed, so nginx removing <c>/api</c> does not matter. A timestamp more than five
/// minutes off is refused, so that a captured call cannot be replayed later.
/// </summary>
internal sealed class IdentityWebhookVerifier : IIdentityWebhookVerifier
{
    public const string TimestampHeader = "X-Identity-Timestamp";
    public const string SignatureHeader = "X-Identity-Signature";
    public const string SignaturePrefix = "sha256=";

    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);

    private readonly IOptions<IdentityConfiguration> _options;
    private readonly IDateTimeProvider _clock;

    public IdentityWebhookVerifier(IOptions<IdentityConfiguration> options, IDateTimeProvider clock)
    {
        _options = options;
        _clock = clock;
    }

    public bool IsSigned(HttpRequest request, byte[] body)
    {
        var settings = _options.Value;

        // Without a secret nobody can sign anything, so every call is refused rather than accepted unchecked
        if (string.IsNullOrEmpty(settings.WebhookSecret))
        {
            return false;
        }

        var timestampText = request.Headers[TimestampHeader].ToString();
        var signature = request.Headers[SignatureHeader].ToString();

        if (!long.TryParse(timestampText, NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp)
            || !IsFresh(timestamp))
        {
            return false;
        }

        var expected = Sign(settings.WebhookSecret, timestampText, body);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signature),
            Encoding.UTF8.GetBytes(expected));
    }

    private bool IsFresh(long timestamp)
    {
        if (timestamp > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
        {
            return false;
        }

        var age = _clock.UtcNow - DateTimeOffset.FromUnixTimeSeconds(timestamp);

        return age.Duration() <= MaxAge;
    }

    public static string Sign(string secret, string timestamp, byte[] body)
    {
        byte[] signed = [.. Encoding.UTF8.GetBytes($"{timestamp}."), .. body];
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signed);

        return SignaturePrefix + Convert.ToHexStringLower(hash);
    }
}
