using PartyKing.Domain.Entities.Auth;

namespace PartyKing.Application.Accounts;

/// <summary>Who is signed in: what the API puts in the account cookie (the stamp is what a password change or a sign-out everywhere invalidates).</summary>
public sealed record AccountIdentity(Guid Id, AccountRole Role, Guid SecurityStamp);
