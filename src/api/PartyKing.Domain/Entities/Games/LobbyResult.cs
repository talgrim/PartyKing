namespace PartyKing.Domain.Entities.Games;

public record LobbyResult<TGame>(LobbyStatus Status, TGame? Game);
