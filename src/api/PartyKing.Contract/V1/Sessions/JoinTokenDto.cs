namespace PartyKing.Contract.V1.Sessions;

/// <param name="Token">Signed token that unlocks the session until <paramref name="ExpiresAt"/>.</param>
/// <param name="ExpiresAt">When the token stops working.</param>
public record JoinTokenDto(string Token, DateTimeOffset ExpiresAt);
