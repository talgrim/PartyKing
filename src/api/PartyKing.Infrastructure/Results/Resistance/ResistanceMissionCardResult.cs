using PartyKing.Domain.Entities.Games;

namespace PartyKing.Infrastructure.Results;

public record ResistanceMissionCardResult(ResistanceMissionStatus Status, ResistanceGame? Game) : IResistanceResult;
