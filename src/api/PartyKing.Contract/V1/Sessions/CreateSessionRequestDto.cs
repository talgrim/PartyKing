namespace PartyKing.Contract.V1.Sessions;

/// <param name="Name">Optional; defaults to "New session".</param>
/// <param name="Password">Required; everyone who joins the session has to enter it.</param>
public record CreateSessionRequestDto(string? Name, string? Password);
