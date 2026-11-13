namespace PartyKing.Application.Demo.Models;

/// <summary>What a started demo session hands to the caller, who stores it in cookies.</summary>
/// <param name="ProfileId">The synthetic host the session belongs to.</param>
/// <param name="AccessStamp">What unlocks the session, so the caller needs no password.</param>
public record DemoStart(
    Guid SessionId,
    string ProfileId,
    string AccessStamp,
    DateTimeOffset ExpiresAt);
