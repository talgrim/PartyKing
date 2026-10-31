using PartyKing.Domain.Entities.Games;

namespace PartyKing.Infrastructure.Results;

/// <summary>What every Resistance repository action answers with: a status of its own and the game after the action.</summary>
public interface IResistanceResult
{
    /// <summary>The game as saved by the action; null when the action did not succeed.</summary>
    ResistanceGame? Game { get; }
}
