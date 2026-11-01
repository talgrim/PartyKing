namespace PartyKing.Application.Ports;

public interface IOAuthStatesRepository
{
    /// <summary>Creates a short-lived state value bound to the session being claimed.</summary>
    Task<string> CreateAsync(Guid sessionId, TimeSpan lifetime, CancellationToken cancellationToken);

    /// <summary>Single use: returns the session id and deletes the state, or null when it is unknown or expired.</summary>
    Task<Guid?> ConsumeAsync(string state, CancellationToken cancellationToken);
}
