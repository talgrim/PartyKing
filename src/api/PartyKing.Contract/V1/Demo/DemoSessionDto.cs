namespace PartyKing.Contract.V1.Demo;

/// <param name="ExpiresAt">When the server deletes the session.</param>
/// <param name="ServerTime">The server's clock at the moment of the answer.</param>
public record DemoSessionDto(DateTimeOffset ExpiresAt, DateTimeOffset ServerTime);
