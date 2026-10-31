using PartyKing.Domain.Entities.Games;

namespace PartyKing.Infrastructure.Results;

public record RingoContinueResult(RingoContinueStatus Status, RingoGame? Game);
