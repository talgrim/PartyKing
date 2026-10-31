namespace PartyKing.Infrastructure.Results;

public record LobbyResult<TGame>(LobbyStatus Status, TGame? Game);
