namespace PartyKing.API.Authentication;

/// <summary>Names of the authentication schemes registered in <c>DependencyInjection</c>.</summary>
public static class SchemeNames
{
    /// <summary>The host's Spotify login cookie.</summary>
    public const string Host = "CustomCookie";

    /// <summary>The account cookie of a user or an admin.</summary>
    public const string Account = "AccountCookie";
}
