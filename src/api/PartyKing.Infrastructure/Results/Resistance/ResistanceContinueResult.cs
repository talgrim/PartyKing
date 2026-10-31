using PartyKing.Domain.Entities.Games;

namespace PartyKing.Infrastructure.Results;

public record ResistanceContinueResult(ResistanceContinueStatus Status, ResistanceGame? Game) : IResistanceResult;
