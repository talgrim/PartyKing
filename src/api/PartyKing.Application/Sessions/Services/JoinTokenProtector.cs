using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using PartyKing.Application.System;

namespace PartyKing.Application.Sessions.Services;

/// <param name="SessionId">The session the token unlocks.</param>
/// <param name="AccessStamp">The session's access stamp when the token was issued.</param>
public record JoinTokenPayload(Guid SessionId, string AccessStamp);

/// <summary>Signs and reads the stateless tokens behind the "join with QR code" links.</summary>
public interface IJoinTokenProtector
{
    string Protect(JoinTokenPayload payload, DateTimeOffset expiresAt);

    /// <summary>The payload, or null when the token is forged, malformed or expired.</summary>
    JoinTokenPayload? Unprotect(string token);
}

internal class JoinTokenProtector : IJoinTokenProtector
{
    private const char Separator = '|';

    private readonly IDataProtector _protector;
    private readonly IDateTimeProvider _clock;

    public JoinTokenProtector(IDataProtectionProvider dataProtectionProvider, IDateTimeProvider clock)
    {
        _protector = dataProtectionProvider.CreateProtector("PartyKing.SessionJoinToken");
        _clock = clock;
    }

    public string Protect(JoinTokenPayload payload, DateTimeOffset expiresAt)
    {
        var expiry = expiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        return _protector.Protect(string.Join(Separator, payload.SessionId, payload.AccessStamp, expiry));
    }

    public JoinTokenPayload? Unprotect(string token)
    {
        string plain;

        try
        {
            plain = _protector.Unprotect(token);
        }
        catch (CryptographicException)
        {
            return null;
        }

        var parts = plain.Split(Separator);

        if (parts.Length != 3
            || !Guid.TryParse(parts[0], out var sessionId)
            || !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var expiry)
            || _clock.UtcNow.ToUnixTimeSeconds() >= expiry)
        {
            return null;
        }

        return new JoinTokenPayload(sessionId, parts[1]);
    }
}
