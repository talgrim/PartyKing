using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>
/// What a Resistance action answers with: why it did not happen (null when it did) and the game it ran on, which is
/// null only when the session has no game. An action that did happen changed the game; one that did not changed
/// nothing, so the repository saves exactly the successful ones.
/// </summary>
public sealed record ResistanceOutcome(ResistanceFailure? Failure, ResistanceGame? Game) : IGameUpdateResult
{
    public static ResistanceOutcome NoGame { get; } = new(ResistanceFailure.NoGame, null);

    public bool Changed => Failure is null;

    public static ResistanceOutcome Success(ResistanceGame game) => new(null, game);

    public static ResistanceOutcome Failed(ResistanceFailure failure, ResistanceGame game) => new(failure, game);
}
