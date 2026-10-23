namespace PartyKing.Contract.V1.Sessions;

/// <param name="Code">Short code guests can search for on the landing page.</param>
/// <param name="HostName">Spotify display name of the host; null until the session is claimed.</param>
public record SessionDto(Guid Id, string Code, string Name, string? HostName, bool HasHost);
