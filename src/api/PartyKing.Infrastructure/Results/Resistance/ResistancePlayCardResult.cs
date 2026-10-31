using PartyKing.Domain.Entities.Games;

namespace PartyKing.Infrastructure.Results;

public record ResistancePlayCardResult(ResistancePlayCardStatus Status, ResistanceGame? Game);
