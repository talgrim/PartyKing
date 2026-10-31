using PartyKing.Domain.Entities.Games;

namespace PartyKing.Infrastructure.Results;

public record ResistanceRestartResult(ResistanceRestartStatus Status, ResistanceGame? Game);
