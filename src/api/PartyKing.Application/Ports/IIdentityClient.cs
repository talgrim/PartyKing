using PartyKing.Contract.V1.Enums;

namespace PartyKing.Application.Ports;

/// <summary>What the identity service (auth.talgrim.dev) said about the caller's session cookie.</summary>
public abstract record IdentityResult
{
    private IdentityResult()
    {
    }

    /// <summary>No session cookie, or one the identity service does not accept (expired, signed out, unknown).</summary>
    public sealed record Anonymous : IdentityResult
    {
        public static readonly Anonymous Instance = new();
    }

    /// <summary>A signed-in account that meets the role the caller asked for.</summary>
    public sealed record Account(Guid Id, string Username, AccountRole Role) : IdentityResult;

    /// <summary>A signed-in account that does not have the role the caller asked for.</summary>
    public sealed record Forbidden : IdentityResult
    {
        public static readonly Forbidden Instance = new();
    }

    /// <summary>The identity service could not be reached or answered with an error. Says nothing about the caller.</summary>
    public sealed record Unavailable : IdentityResult
    {
        public static readonly Unavailable Instance = new();
    }
}

public interface IIdentityClient
{
    /// <summary>
    /// Asks who the session cookie in <paramref name="cookieHeader"/> belongs to, optionally requiring a role. A request
    /// without the session cookie is answered <see cref="IdentityResult.Anonymous"/> without a call. Never throws for an
    /// unreachable service: that is <see cref="IdentityResult.Unavailable"/>.
    /// </summary>
    Task<IdentityResult> GetCallerAsync(string? cookieHeader, AccountRole? requiredRole, CancellationToken cancellationToken);
}
