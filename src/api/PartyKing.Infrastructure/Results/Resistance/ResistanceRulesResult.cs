using PartyKing.Domain.Entities.Games;

namespace PartyKing.Infrastructure.Results;

public record ResistanceRulesResult(ResistanceRulesStatus Status, ResistanceGame? Game);
