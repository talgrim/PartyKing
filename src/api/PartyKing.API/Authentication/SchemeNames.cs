namespace PartyKing.API.Authentication;

/// <summary>Names of the authentication schemes registered in <c>DependencyInjection</c>.</summary>
public static class SchemeNames
{
    /// <summary>The host's Spotify login cookie.</summary>
    public const string Host = "CustomCookie";

    public const string Admin = "AdminCookie";
}
