namespace PartyKing.Contract.V1.Demo;

/// <param name="Enabled">Whether this server is a demo: visitors can start a throw-away session of their own.</param>
/// <param name="ServerTime">The server's clock, so the client can count down to <see cref="DemoSessionDto.ExpiresAt"/> without trusting its own.</param>
public record DemoConfigDto(bool Enabled, DateTimeOffset ServerTime);
