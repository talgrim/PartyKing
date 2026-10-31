using PartyKing.Domain.Entities.Games;

namespace PartyKing.Infrastructure.Results;

public record ResistanceDealResult(ResistanceDealStatus Status, ResistanceGame? Game);
