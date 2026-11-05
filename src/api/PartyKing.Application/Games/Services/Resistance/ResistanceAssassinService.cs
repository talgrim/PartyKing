using ErrorOr;
using PartyKing.Application.Games.Services.Base;
using PartyKing.Contract.V1.Games.Resistance;
using PartyKing.Domain.Entities.Games.Resistance;
using PartyKing.Domain.Repositories;

namespace PartyKing.Application.Games.Services.Resistance;

/// <summary>The Assassin module of a Resistance game: naming the Commander after the third successful mission.</summary>
public interface IResistanceAssassinService
{
    /// <summary>
    /// The Assassin names the player they take for the Commander, which ends the game. Forbidden for anyone else,
    /// Conflict outside the Assassination, Validation for a player who can't be named.
    /// </summary>
    Task<ErrorOr<ResistanceGameDto>> AssassinateAsync(
        Guid sessionId, Guid targetPlayerId, GameCaller caller, CancellationToken cancellationToken);
}

internal class ResistanceAssassinService : IResistanceAssassinService
{
    private readonly ResistanceActionGate _gate;
    private readonly IResistanceGamesRepository _games;

    public ResistanceAssassinService(ResistanceActionGate gate, IResistanceGamesRepository games)
    {
        _gate = gate;
        _games = games;
    }

    public Task<ErrorOr<ResistanceGameDto>> AssassinateAsync(
        Guid sessionId, Guid targetPlayerId, GameCaller caller, CancellationToken cancellationToken)
    {
        return _gate.RunAsPlayerAsync(
            sessionId,
            caller,
            _ => _games.UpdateAsync(
                sessionId,
                nameof(AssassinateAsync),
                ResistanceOutcome.NoGame,
                game => game.Assassinate(caller.PlayerId, targetPlayerId),
                cancellationToken),
            cancellationToken);
    }
}
