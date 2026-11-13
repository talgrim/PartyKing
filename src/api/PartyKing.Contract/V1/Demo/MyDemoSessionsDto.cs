namespace PartyKing.Contract.V1.Demo;

/// <summary>A demo session the caller started and can still go back to.</summary>
/// <param name="ExpiresAt">When the server deletes the session.</param>
public record MyDemoSessionDto(Guid SessionId, string Name, DateTimeOffset ExpiresAt);

/// <summary>The caller's own demo sessions that have not ended, the one that ends last first.</summary>
/// <param name="ServerTime">The server's clock at the moment of the answer, for the countdowns.</param>
public record MyDemoSessionsDto(IReadOnlyList<MyDemoSessionDto> Sessions, DateTimeOffset ServerTime);
