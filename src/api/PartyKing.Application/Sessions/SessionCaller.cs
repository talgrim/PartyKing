namespace PartyKing.Application.Sessions;

/// <summary>Who calls a session endpoint: the logged-in host's Spotify profile id, if any, and whether an admin.</summary>
public record SessionCaller(string? ProfileId, bool IsAdmin);
