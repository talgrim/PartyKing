namespace PartyKing.Application.Demo.Models;

/// <summary>What a started demo session hands to the caller, who stores it in cookies.</summary>
/// <param name="ProfileId">The synthetic host the session belongs to.</param>
/// <param name="AccessToken">Stands in for the host's Spotify token, which no demo call uses.</param>
/// <param name="AccessTokenExpiresIn">Seconds; as long as the session lives.</param>
/// <param name="AccessStamp">What unlocks the session, so the caller needs no password.</param>
public record DemoStart(
    Guid SessionId,
    string ProfileId,
    string AccessToken,
    int AccessTokenExpiresIn,
    string AccessStamp,
    DateTimeOffset ExpiresAt);
