namespace PartyKing.Domain.Enums;

public enum SessionClaimResult
{
    Claimed,
    SessionNotFound,
    AlreadyClaimed,
    ProfileHostsAnotherSession
}
