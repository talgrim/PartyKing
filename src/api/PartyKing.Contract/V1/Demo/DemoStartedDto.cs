namespace PartyKing.Contract.V1.Demo;

/// <param name="SessionId">The new session, already unlocked and hosted by the caller.</param>
/// <param name="ExpiresAt">When the server deletes it.</param>
public record DemoStartedDto(Guid SessionId, DateTimeOffset ExpiresAt);
