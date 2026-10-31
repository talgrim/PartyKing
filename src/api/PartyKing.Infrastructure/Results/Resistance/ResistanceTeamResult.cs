using PartyKing.Domain.Entities.Games;

namespace PartyKing.Infrastructure.Results;

public record ResistanceTeamResult(ResistanceTeamStatus Status, ResistanceGame? Game) : IResistanceResult;
