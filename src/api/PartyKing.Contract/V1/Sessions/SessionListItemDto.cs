namespace PartyKing.Contract.V1.Sessions;

/// <summary>
/// A session as the public list shows it. It says whether the session has a host, never who it is.
/// </summary>
/// <param name="Code">Short code guests can search for on the landing page.</param>
/// <param name="HasHost">Whether someone has claimed the session as its host.</param>
public record SessionListItemDto(Guid Id, string Code, string Name, bool HasHost);
