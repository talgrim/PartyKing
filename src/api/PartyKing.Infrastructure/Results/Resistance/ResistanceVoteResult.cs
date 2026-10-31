using PartyKing.Domain.Entities.Games;

namespace PartyKing.Infrastructure.Results;

public record ResistanceVoteResult(ResistanceVoteStatus Status, ResistanceGame? Game) : IResistanceResult;
