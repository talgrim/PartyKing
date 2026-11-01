namespace PartyKing.Domain.Entities.Games;

/// <summary>
/// What a game action reports back to the repository that ran it: whether it changed the game, so the repository
/// knows if there is anything to save. An action that failed a guard changes nothing.
/// </summary>
public interface IGameUpdateResult
{
    bool Changed { get; }
}
