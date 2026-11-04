namespace PartyKing.Domain.Enums;

/// <summary>What <c>Session.ClaimHost</c> decided.</summary>
public enum HostClaimOutcome
{
    /// <summary>The session had no host and the profile is its host now.</summary>
    Claimed,

    /// <summary>The profile already hosts the session (logging in again); nothing changed.</summary>
    AlreadyHost,

    /// <summary>Another profile hosts the session; nothing changed.</summary>
    HostedByAnother
}
