namespace PartyKing.Contract.V1.Sessions;

/// <param name="Name">Optional; defaults to "New session".</param>
public record CreateSessionRequest(string? Name);
