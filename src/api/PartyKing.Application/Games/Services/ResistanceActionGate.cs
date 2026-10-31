using ErrorOr;
using PartyKing.Application.Mappings;
using PartyKing.Application.Realtime;
using PartyKing.Contract.V1.Games.Resistance;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Results;

namespace PartyKing.Application.Games.Services;

/// <summary>
/// The frame every Resistance action shares: check who may act, call the repository, turn its status into an error,
/// tell the session's clients and build the caller's view of the game.
/// </summary>
internal sealed class ResistanceActionGate
{
    private readonly GameLobby<ResistanceGame, ResistancePlayer> _lobby;
    private readonly ISessionMusicNotifier _notifier;

    public ResistanceActionGate(GameLobby<ResistanceGame, ResistancePlayer> lobby, ISessionMusicNotifier notifier)
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
        Func<GameLobby<ResistanceGame, ResistancePlayer>.Access, Task<TResult>> call,
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

        return await FinishAsync(sessionId, caller, await call(access.Value), mapError, cancellationToken);
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

        return await FinishAsync(sessionId, caller, await call(), mapError, cancellationToken);
    }

    private async Task<ErrorOr<ResistanceGameDto>> FinishAsync<TResult>(
        Guid sessionId,
        GameCaller caller,
        TResult result,
        Func<TResult, Error?> mapError,
        CancellationToken cancellationToken)
        where TResult : IResistanceResult
    {
        if (mapError(result) is { } error)
        {
            return error;
        }

        await _notifier.ResistanceChangedAsync(sessionId, cancellationToken);

        return ResistanceGameViewBuilder.Build(result.Game!, caller);
    }
}
