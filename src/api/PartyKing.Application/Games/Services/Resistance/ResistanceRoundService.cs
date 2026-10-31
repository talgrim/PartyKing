using ErrorOr;
using PartyKing.Application.System;
using PartyKing.Contract.V1.Games.Resistance;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Application.Games.Services.Resistance;

/// <summary>The course of a Resistance game: start, team, vote, mission and moving on.</summary>
public interface IResistanceRoundService
{
    /// <summary>
    /// Host or admin: starts the open game once at least 5 players joined, which deals the roles, picks the first
    /// leader and locks the players. NotFound without a game, Conflict while it is closed, started or short of players.
    /// </summary>
    Task<ErrorOr<ResistanceGameDto>> StartAsync(Guid sessionId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>
    /// Host or admin: starts a finished game over with the same players (new roles and leader, mission 1). NotFound
    /// without a game, Conflict before the start and while the game is still being played.
    /// </summary>
    Task<ErrorOr<ResistanceGameDto>> RestartAsync(Guid sessionId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>
    /// The mission leader submits the team of the current mission: exactly <c>RequiredTeamSize</c> distinct players,
    /// which locks it. Forbidden for anyone but the leader (the host and admins get no override), NotFound without a
    /// game, Conflict before the start or once a team was submitted, Validation for a wrong size or unknown player.
    /// </summary>
    Task<ErrorOr<ResistanceGameDto>> SelectTeamAsync(
        Guid sessionId, IReadOnlyList<Guid>? playerIds, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>
    /// A player votes on the submitted team, and may change the vote until the last player has voted. Forbidden for
    /// anyone who did not join (the host and admins too), NotFound without a game, Conflict before the start, when
    /// no team is up for a vote or when the game is over.
    /// </summary>
    Task<ErrorOr<ResistanceGameDto>> VoteAsync(
        Guid sessionId, bool approve, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>
    /// A member of the approved team secretly plays Success (true) or Fail (false), once. Forbidden for anyone who did
    /// not join, who is not on the team, or is a member of the resistance playing Fail; Conflict before the start, when
    /// no approved team is playing, when the caller already played, and when the game is over.
    /// </summary>
    Task<ErrorOr<ResistanceGameDto>> PlayMissionCardAsync(
        Guid sessionId, bool success, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>
    /// Moves on after a rejected team or a finished mission (next leader, new team, next mission). The leader, the host
    /// and admins may; Forbidden for other players, Conflict unless the votes rejected the team or the mission is over.
    /// </summary>
    Task<ErrorOr<ResistanceGameDto>> ContinueAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken);
}

internal class ResistanceRoundService : IResistanceRoundService
{
    private readonly ResistanceActionGate _gate;
    private readonly IResistanceGamesRepository _games;
    private readonly IDateTimeProvider _clock;

    public ResistanceRoundService(ResistanceActionGate gate, IResistanceGamesRepository games, IDateTimeProvider clock)
    {
        _gate = gate;
        _games = games;
        _clock = clock;
    }

    public Task<ErrorOr<ResistanceGameDto>> StartAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        return _gate.RunAsManagerAsync(
            sessionId,
            caller,
            () => _games.StartAsync(sessionId, _clock.UtcNow, cancellationToken),
            result => ResistanceErrors.From(result.Status),
            cancellationToken);
    }

    public Task<ErrorOr<ResistanceGameDto>> RestartAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        return _gate.RunAsManagerAsync(
            sessionId,
            caller,
            () => _games.RestartAsync(sessionId, cancellationToken),
            result => ResistanceErrors.From(result.Status),
            cancellationToken);
    }

    public Task<ErrorOr<ResistanceGameDto>> SelectTeamAsync(
        Guid sessionId, IReadOnlyList<Guid>? playerIds, GameCaller caller, CancellationToken cancellationToken)
    {
        return _gate.RunAsPlayerAsync(
            sessionId,
            caller,
            _ => _games.SelectTeamAsync(sessionId, caller.PlayerId, playerIds ?? [], cancellationToken),
            result => ResistanceErrors.From(result.Status, result.Game),
            cancellationToken);
    }

    public Task<ErrorOr<ResistanceGameDto>> VoteAsync(
        Guid sessionId, bool approve, GameCaller caller, CancellationToken cancellationToken)
    {
        return _gate.RunAsPlayerAsync(
            sessionId,
            caller,
            _ => _games.VoteAsync(sessionId, caller.PlayerId, approve, cancellationToken),
            result => ResistanceErrors.From(result.Status),
            cancellationToken);
    }

    public Task<ErrorOr<ResistanceGameDto>> PlayMissionCardAsync(
        Guid sessionId, bool success, GameCaller caller, CancellationToken cancellationToken)
    {
        return _gate.RunAsPlayerAsync(
            sessionId,
            caller,
            _ => _games.PlayMissionCardAsync(sessionId, caller.PlayerId, success, cancellationToken),
            result => ResistanceErrors.From(result.Status),
            cancellationToken);
    }

    public Task<ErrorOr<ResistanceGameDto>> ContinueAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        return _gate.RunAsPlayerAsync(
            sessionId,
            caller,
            access => _games.ContinueAsync(sessionId, caller.PlayerId, access.IsManager, cancellationToken),
            result => ResistanceErrors.From(result.Status),
            cancellationToken);
    }
}
