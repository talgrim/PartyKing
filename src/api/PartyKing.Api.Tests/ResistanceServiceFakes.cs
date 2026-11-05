using ErrorOr;
using PartyKing.Application.Games.Services.Base;
using PartyKing.Application.Games.Services.Resistance;
using PartyKing.Contract.V1.Games.Resistance;

namespace PartyKing.Api.Tests;

/// <summary>What a fake Resistance service answers with and what it was last asked.</summary>
internal abstract class FakeResistanceCalls
{
    public ErrorOr<ResistanceGameDto> Result { get; set; }

    public (string Action, Guid SessionId, bool? Approve, Guid? PlayerId)? LastCall { get; private set; }

    protected Task<ErrorOr<ResistanceGameDto>> Record(
        string action, Guid sessionId, bool? approve, GameCaller caller)
    {
        LastCall = (action, sessionId, approve, caller.PlayerId);
        return Task.FromResult(Result);
    }
}

/// <summary>The controllers only need the lobby service injected; none of the tests call it.</summary>
internal sealed class FakeResistanceLobbyService : IResistanceLobbyService
{
    public Task<ErrorOr<ResistanceGameDto>> GetAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<ErrorOr<ResistanceLobbyDto>> GetLobbyAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<ErrorOr<ResistanceGameDto>> CreateAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<ErrorOr<ResistanceGameDto>> OpenAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<ErrorOr<GameSeat>> JoinAsync(
        Guid sessionId, string? name, GameCaller caller, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<ErrorOr<ResistanceGameDto>> RefreshRulesAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<ErrorOr<ResistanceGameDto>> SetPlotCardsAsync(
        Guid sessionId, bool enabled, GameCaller caller, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<ErrorOr<ResistanceGameDto>> KickAsync(
        Guid sessionId, Guid playerId, GameCaller caller, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<ErrorOr<ResistanceGameDto>> ReorderAsync(
        Guid sessionId, IReadOnlyList<Guid>? playerIds, GameCaller caller, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<ErrorOr<Deleted>> EndAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

internal sealed class FakeResistanceRoundService : FakeResistanceCalls, IResistanceRoundService
{
    public Task<ErrorOr<ResistanceGameDto>> StartAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<ErrorOr<ResistanceGameDto>> RestartAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        return Record("restart", sessionId, null, caller);
    }

    public Task<ErrorOr<ResistanceGameDto>> SelectTeamAsync(
        Guid sessionId, IReadOnlyList<Guid>? playerIds, GameCaller caller, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<ErrorOr<ResistanceGameDto>> VoteAsync(
        Guid sessionId, bool approve, GameCaller caller, CancellationToken cancellationToken)
    {
        return Record("vote", sessionId, approve, caller);
    }

    public Task<ErrorOr<ResistanceGameDto>> PlayMissionCardAsync(
        Guid sessionId, bool success, GameCaller caller, CancellationToken cancellationToken)
    {
        return Record("mission", sessionId, success, caller);
    }

    public Task<ErrorOr<ResistanceGameDto>> ContinueAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        return Record("continue", sessionId, null, caller);
    }
}

internal sealed class FakeResistancePlotCardService : FakeResistanceCalls, IResistancePlotCardService
{
    public Task<ErrorOr<ResistanceGameDto>> DealPlotCardsAsync(
        Guid sessionId,
        IReadOnlyList<ResistancePlotCardAssignmentDto>? assignments,
        GameCaller caller,
        CancellationToken cancellationToken)
    {
        return Record("deal-plot-cards", sessionId, null, caller);
    }

    public Task<ErrorOr<ResistanceGameDto>> PlayPlotCardAsync(
        Guid sessionId, int cardId, Guid targetPlayerId, GameCaller caller, CancellationToken cancellationToken)
    {
        return Record($"play-{cardId}-{targetPlayerId}", sessionId, null, caller);
    }

    public Task<ErrorOr<ResistanceGameDto>> ConfirmPlotCardAsync(
        Guid sessionId, int cardId, GameCaller caller, CancellationToken cancellationToken)
    {
        return Record($"confirm-{cardId}", sessionId, null, caller);
    }

    public Task<ErrorOr<ResistanceGameDto>> TakeResponsibilityAsync(
        Guid sessionId,
        int cardId,
        Guid targetPlayerId,
        bool? takeOpinionMaker,
        GameCaller caller,
        CancellationToken cancellationToken)
    {
        return Record($"take-{cardId}-{targetPlayerId}-{takeOpinionMaker}", sessionId, null, caller);
    }

    public Task<ErrorOr<ResistanceGameDto>> TakeLeadAsync(
        Guid sessionId, int cardId, GameCaller caller, CancellationToken cancellationToken)
    {
        return Record($"take-lead-{cardId}", sessionId, null, caller);
    }

    public Task<ErrorOr<ResistanceGameDto>> PeekMissionCardAsync(
        Guid sessionId, int cardId, Guid targetPlayerId, GameCaller caller, CancellationToken cancellationToken)
    {
        return Record($"peek-{cardId}", sessionId, null, caller);
    }

    public Task<ErrorOr<ResistanceGameDto>> PlayNoConfidenceAsync(
        Guid sessionId, int cardId, GameCaller caller, CancellationToken cancellationToken)
    {
        return Record($"no-confidence-{cardId}", sessionId, null, caller);
    }

    public Task<ErrorOr<ResistanceGameDto>> PassLeaderWindowAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        return Record("pass-leader-window", sessionId, null, caller);
    }
}

internal sealed class FakeResistancePresenceService : IResistancePresenceService
{
    public ErrorOr<ResistancePresenceDto> Result { get; set; } = new ResistancePresenceDto([]);

    public (Guid SessionId, Guid? PlayerId)? LastCall { get; private set; }

    public Task<ErrorOr<ResistancePresenceDto>> GetAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        LastCall = (sessionId, caller.PlayerId);
        return Task.FromResult(Result);
    }
}

/// <summary>Remembers what the seats controller asked of it.</summary>
internal sealed class FakeResistanceSeatService : IResistanceSeatService
{
    public ErrorOr<GameSeat> ClaimResult { get; set; }

    public List<(Guid SessionId, Guid PlayerId)> Claims { get; } = [];

    public (Guid PlayerId, bool Released, GameCaller Caller)? LastRelease { get; private set; }

    public Task<ErrorOr<IReadOnlyList<ResistanceClaimableSeatDto>>> GetClaimableAsync(
        Guid sessionId, CancellationToken cancellationToken) =>
        Task.FromResult(ErrorOrFactory.From<IReadOnlyList<ResistanceClaimableSeatDto>>(
            [new ResistanceClaimableSeatDto(Guid.Empty, "Ola")]));

    public Task<ErrorOr<ResistanceGameDto>> SetReleasedAsync(
        Guid sessionId, Guid playerId, bool released, GameCaller caller, CancellationToken cancellationToken)
    {
        LastRelease = (playerId, released, caller);

        return Task.FromResult(ErrorOrFactory.From(new ResistanceGameDto { Rules = new ResistanceRulesDto([], false, 1) }));
    }

    public Task<ErrorOr<GameSeat>> ClaimAsync(Guid sessionId, Guid playerId, CancellationToken cancellationToken)
    {
        Claims.Add((sessionId, playerId));

        return Task.FromResult(ClaimResult);
    }

    public Task<bool> HoldsSeatAsync(Guid sessionId, GameSeat seat, CancellationToken cancellationToken) =>
        Task.FromResult(true);
}
