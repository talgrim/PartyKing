using PartyKing.Domain.Entities.Games;

namespace PartyKing.Infrastructure.Results;

public record RingoFlipResult(RingoFlipStatus Status, RingoGame? Game);
