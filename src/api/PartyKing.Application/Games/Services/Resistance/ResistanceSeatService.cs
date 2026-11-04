using ErrorOr;
using PartyKing.Application.Games.Services.Base;
using PartyKing.Application.Realtime;
using PartyKing.Contract.V1.Games.Resistance;
using PartyKing.Domain.Entities.Games.Base;
using PartyKing.Domain.Entities.Games.Resistance;
using PartyKing.Domain.Repositories;

namespace PartyKing.Application.Games.Services.Resistance;

/// <summary>
/// Taking a seat over in a started game: the host or an admin releases the seat of a player who is gone, and a guest
/// claims it from the join page (see <see cref="ResistanceGame.ClaimSeat"/>).
/// </summary>
public interface IResistanceSeatService
{
    /// <summary>
    /// The seats a guest may claim now: released and with no device connected. Empty for a game that has not started;
    /// NotFound without a game. For anyone who unlocked the session, as the players' names are public to them.
    /// </summary>
    Task<ErrorOr<IReadOnlyList<ResistanceClaimableSeatDto>>> GetClaimableAsync(
        Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Host or admin: releases the seat of an offline player (or takes the release back). NotFound without a game or
    /// for an unknown player, Conflict for the seat of the host or an admin, a player who is online or a game that
    /// has not started.
    /// </summary>
    Task<ErrorOr<ResistanceGameDto>> SetReleasedAsync(
        Guid sessionId, Guid playerId, bool released, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>
    /// Takes a released seat over and returns it with its new stamp, which the caller's cookie has to carry. Conflict
    /// for a seat that was not released, whose player is connected, or a game that has not started.
    /// </summary>
    Task<ErrorOr<GameSeat>> ClaimAsync(
        Guid sessionId, Guid playerId, CancellationToken cancellationToken);

    /// <summary>Whether the seat still belongs to the device that holds it (the player is in the game and the stamp is the seat's).</summary>
    Task<bool> HoldsSeatAsync(Guid sessionId, GameSeat seat, CancellationToken cancellationToken);
}

internal sealed class ResistanceSeatService : IResistanceSeatService
{
    private readonly ResistanceActionGate _gate;
    private readonly IResistanceGamesRepository _games;
    private readonly IGamePresence _presence;

    public ResistanceSeatService(ResistanceActionGate gate, IResistanceGamesRepository games, IGamePresence presence)
    {
        _gate = gate;
        _games = games;
        _presence = presence;
    }

    public async Task<ErrorOr<IReadOnlyList<ResistanceClaimableSeatDto>>> GetClaimableAsync(
        Guid sessionId, CancellationToken cancellationToken)
    {
        var game = await _games.GetAsync(sessionId, cancellationToken);
        if (game is null)
        {
            return ResistanceErrors.NoGame;
        }

        if (!game.IsStarted)
        {
            return new List<ResistanceClaimableSeatDto>();
        }

        var online = _presence.OnlinePlayers(GameKind.Resistance, sessionId);

        return game.Players
            .Where(x => x.IsReclaimable && !x.IsManagerSeat && !online.Contains(x.PlayerId))
            .Select(x => new ResistanceClaimableSeatDto(x.PlayerId, x.DisplayName))
            .ToList();
    }

    public Task<ErrorOr<ResistanceGameDto>> SetReleasedAsync(
        Guid sessionId, Guid playerId, bool released, GameCaller caller, CancellationToken cancellationToken)
    {
        return _gate.RunAsManagerAsync(
            sessionId,
            caller,
            () => _games.UpdateAsync(
                sessionId,
                nameof(SetReleasedAsync),
                ResistanceOutcome.NoGame,
                game => game.SetSeatReleased(playerId, released, IsOnline(sessionId, playerId)),
                cancellationToken),
            cancellationToken);
    }

    public async Task<ErrorOr<GameSeat>> ClaimAsync(
        Guid sessionId, Guid playerId, CancellationToken cancellationToken)
    {
        var stamp = Guid.NewGuid();
        var outcome = await _games.UpdateAsync(
            sessionId,
            nameof(ClaimAsync),
            ResistanceOutcome.NoGame,
            game => game.ClaimSeat(playerId, IsOnline(sessionId, playerId), stamp),
            cancellationToken);

        return ResistanceErrors.From(outcome) is { } error ? error : new GameSeat(playerId, stamp);
    }

    public async Task<bool> HoldsSeatAsync(Guid sessionId, GameSeat seat, CancellationToken cancellationToken)
    {
        var game = await _games.GetAsync(sessionId, cancellationToken);

        return game is not null && game.Players.Any(x => x.PlayerId == seat.PlayerId && x.IsHeldBy(seat.Stamp));
    }

    private bool IsOnline(Guid sessionId, Guid playerId)
    {
        return _presence.OnlinePlayers(GameKind.Resistance, sessionId).Contains(playerId);
    }
}
