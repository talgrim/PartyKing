using PartyKing.Domain.Entities.Games;

namespace PartyKing.Infrastructure.Results;

public record ResistanceStartResult(ResistanceStartStatus Status, ResistanceGame? Game) : IResistanceResult;
