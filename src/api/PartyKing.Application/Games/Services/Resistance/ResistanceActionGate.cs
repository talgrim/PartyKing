using ErrorOr;
using PartyKing.Application.Mappings;
using PartyKing.Application.Realtime;
using PartyKing.Contract.V1.Games.Resistance;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Results;

namespace PartyKing.Application.Games.Services.Resistance;

/// <summary>
/// The frame every Resistance action shares: check who may act, call the repository, turn its status into an error,
/// tell the session's clients and build the caller's view of the game.
/// </summary>
internal sealed class ResistanceActionGate
{
    private readonly ResistanceGameLobby _lobby;
    private readonly ISessionMusicNotifier _notifier;

    public ResistanceActionGate(ResistanceGameLobby lobby, ISessionMusicNotifier notifier)
    {
        _lobby = lobby;
        _notifier = notifier;
    }

    /// <summary>
    /// For the host, admins and the players of the game (<see cref="GameLobby{TGame,TPlayer}.AccessAsync"/>); NotFound
    /// without a game. <paramref name="call"/> gets the access, for actions that treat managers differently.
    /// </summary>
    public async Task<ErrorOr<ResistanceGameDto>> RunAsPlayerAsync<TResult>(
        Guid sessionId,
        GameCaller caller,
        Func<ResistanceGameLobby.Access, Task<TResult>> call,
        Func<TResult, Error?> mapError,
        CancellationToken cancellationToken)
        where TResult : IResistanceResult
    {
        var access = await _lobby.AccessAsync(sessionId, caller, cancellationToken);
        if (access.IsError)
        {
            return access.Errors;
        }

        if (access.Value.Game is null)
        {
            return ResistanceErrors.NoGame;
        }

        var result = await call(access.Value);

        return await FinishAsync(sessionId, caller, mapError(result), result.Game, cancellationToken);
    }

    /// <summary>
    /// <see cref="RunAsPlayerAsync{TResult}"/> for an action that answers with a <see cref="ResistanceOutcome"/>,
    /// which names its own error.
    /// </summary>
    public async Task<ErrorOr<ResistanceGameDto>> RunAsPlayerAsync(
        Guid sessionId,
        GameCaller caller,
        Func<ResistanceGameLobby.Access, Task<ResistanceOutcome>> call,
        CancellationToken cancellationToken)
    {
        var access = await _lobby.AccessAsync(sessionId, caller, cancellationToken);
        if (access.IsError)
        {
            return access.Errors;
        }

        if (access.Value.Game is null)
        {
            return ResistanceErrors.NoGame;
        }

        var outcome = await call(access.Value);

        return await FinishAsync(sessionId, caller, ResistanceErrors.From(outcome), outcome.Game, cancellationToken);
    }

    /// <summary>For the host and admins only (<see cref="GameLobby{TGame,TPlayer}.AuthorizeAsync"/>).</summary>
    public async Task<ErrorOr<ResistanceGameDto>> RunAsManagerAsync<TResult>(
        Guid sessionId,
        GameCaller caller,
        Func<Task<TResult>> call,
        Func<TResult, Error?> mapError,
        CancellationToken cancellationToken)
        where TResult : IResistanceResult
    {
        if (await _lobby.AuthorizeAsync(sessionId, caller, cancellationToken) is { IsError: true } denied)
        {
            return denied.Errors;
        }

        var result = await call();

        return await FinishAsync(sessionId, caller, mapError(result), result.Game, cancellationToken);
    }

    /// <summary>
    /// <see cref="RunAsManagerAsync{TResult}"/> for an action that answers with a <see cref="ResistanceOutcome"/>,
    /// which names its own error.
    /// </summary>
    public async Task<ErrorOr<ResistanceGameDto>> RunAsManagerAsync(
        Guid sessionId,
        GameCaller caller,
        Func<Task<ResistanceOutcome>> call,
        CancellationToken cancellationToken)
    {
        if (await _lobby.AuthorizeAsync(sessionId, caller, cancellationToken) is { IsError: true } denied)
        {
            return denied.Errors;
        }

        var outcome = await call();

        return await FinishAsync(sessionId, caller, ResistanceErrors.From(outcome), outcome.Game, cancellationToken);
    }

    private async Task<ErrorOr<ResistanceGameDto>> FinishAsync(
        Guid sessionId,
        GameCaller caller,
        Error? error,
        ResistanceGame? game,
        CancellationToken cancellationToken)
    {
        if (error is { } failed)
        {
            return failed;
        }

        await _notifier.ResistanceChangedAsync(sessionId, cancellationToken);

        return ResistanceGameViewBuilder.Build(game!, ResistanceGameLobby.PlayerOf(game!, caller));
    }
}
