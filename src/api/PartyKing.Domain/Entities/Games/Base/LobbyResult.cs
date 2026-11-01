namespace PartyKing.Domain.Entities.Games.Base;

public record LobbyResult<TGame>(LobbyStatus Status, TGame? Game);
