using PartyKing.Domain.Repositories;
using ErrorOr;
using PartyKing.Application.Games.Services.Base;
using PartyKing.Contract.V1.Games.Resistance;
using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Application.Games.Services.Resistance;

/// <summary>The Inquisitor module of a Resistance game: examining a player after missions 2, 3 and 4.</summary>
public interface IResistanceInquisitorService
{
    /// <summary>
    /// The holder of the token picks a player who never held it; the token passes to them. Forbidden for anyone else,
    /// Conflict outside the Inquisition, Validation for a player who can't be examined.
    /// </summary>
    Task<ErrorOr<ResistanceGameDto>> ExamineAsync(
        Guid sessionId, Guid targetPlayerId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>The examiner confirms they saw the loyalty, which ends the Inquisition.</summary>
    Task<ErrorOr<ResistanceGameDto>> ConfirmInquisitionAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken);
}

internal class ResistanceInquisitorService : IResistanceInquisitorService
{
    private readonly ResistanceActionGate _gate;
    private readonly IResistanceGamesRepository _games;

    public ResistanceInquisitorService(ResistanceActionGate gate, IResistanceGamesRepository games)
    {
        _gate = gate;
        _games = games;
    }

    public Task<ErrorOr<ResistanceGameDto>> ExamineAsync(
        Guid sessionId, Guid targetPlayerId, GameCaller caller, CancellationToken cancellationToken)
    {
        return RunAsync(
            sessionId,
            caller,
            nameof(ExamineAsync),
            game => game.Examine(caller.PlayerId, targetPlayerId),
            cancellationToken);
    }

    public Task<ErrorOr<ResistanceGameDto>> ConfirmInquisitionAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        return RunAsync(
            sessionId,
            caller,
            nameof(ConfirmInquisitionAsync),
            game => game.ConfirmInquisition(caller.PlayerId),
            cancellationToken);
    }

    private Task<ErrorOr<ResistanceGameDto>> RunAsync(
        Guid sessionId,
        GameCaller caller,
        string operation,
        Func<ResistanceGame, ResistanceOutcome> action,
        CancellationToken cancellationToken)
    {
        return _gate.RunAsPlayerAsync(
            sessionId,
            caller,
            _ => _games.UpdateAsync(sessionId, operation, ResistanceOutcome.NoGame, action, cancellationToken),
            cancellationToken);
    }
}
