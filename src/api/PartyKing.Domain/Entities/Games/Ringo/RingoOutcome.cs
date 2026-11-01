using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.Domain.Entities.Games.Ringo;

/// <summary>
/// What a Ringo action answers with: why it did not happen (null when it was accepted), the game it ran on (null only
/// when the session has no game) and whether it changed the game, which is what the repository saves. An accepted
/// action may change nothing: flipping a card that is flipped already, or continuing when no card is pending.
/// </summary>
public sealed record RingoOutcome(RingoFailure? Failure, RingoGame? Game, bool Changed) : IGameUpdateResult
{
    public static RingoOutcome NoGame { get; } = new(RingoFailure.NoGame, null, false);

    /// <summary>The action was accepted and changed the game.</summary>
    public static RingoOutcome Done(RingoGame game) => new(null, game, true);

    /// <summary>The action was accepted and there was nothing to change.</summary>
    public static RingoOutcome Unchanged(RingoGame game) => new(null, game, false);

    public static RingoOutcome Failed(RingoFailure failure, RingoGame game) => new(failure, game, false);
}
