using ErrorOr;
using PartyKing.Contract.V1.Games.Resistance;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Infrastructure.Results;

namespace PartyKing.Application.Games.Services.Resistance;

/// <summary>The plot cards add-on of a Resistance game: dealing the cards, playing them and the windows around them.</summary>
public interface IResistancePlotCardService
{
    /// <summary>
    /// The mission leader hands the drawn plot cards to the other players. Forbidden for anyone else (no manager
    /// override), Conflict when there is nothing to hand out, Validation for a wrong or incomplete deal.
    /// </summary>
    Task<ErrorOr<ResistanceGameDto>> DealPlotCardsAsync(
        Guid sessionId, IReadOnlyList<ResistancePlotCardAssignmentDto>? assignments, GameCaller caller,
        CancellationToken cancellationToken);

    /// <summary>The holder plays an immediate plot card on a player.</summary>
    Task<ErrorOr<ResistanceGameDto>> PlayPlotCardAsync(
        Guid sessionId, int cardId, Guid targetPlayerId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>The player who saw the identity card confirms it; the last confirmation moves on to the team.</summary>
    Task<ErrorOr<ResistanceGameDto>> ConfirmPlotCardAsync(
        Guid sessionId, int cardId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>
    /// The holder of Take Responsibility takes a plot card from another player; Validation when the choice between a
    /// face-up Opinion Maker and a hand card is missing.
    /// </summary>
    Task<ErrorOr<ResistanceGameDto>> TakeResponsibilityAsync(
        Guid sessionId, int cardId, Guid targetPlayerId, bool? takeOpinionMaker, GameCaller caller,
        CancellationToken cancellationToken);

    /// <summary>In the leader window the holder of Strong Leader takes the lead.</summary>
    Task<ErrorOr<ResistanceGameDto>> TakeLeadAsync(
        Guid sessionId, int cardId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>The holder of Keeping a Close Eye looks at a team member's mission card after the mission.</summary>
    Task<ErrorOr<ResistanceGameDto>> PeekMissionCardAsync(
        Guid sessionId, int cardId, Guid targetPlayerId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>In the confidence window the holder of No Confidence cancels the approved team.</summary>
    Task<ErrorOr<ResistanceGameDto>> PlayNoConfidenceAsync(
        Guid sessionId, int cardId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>A player confirms in the leader or confidence window that they play no card; the last confirmation ends it.</summary>
    Task<ErrorOr<ResistanceGameDto>> PassLeaderWindowAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken);
}

internal class ResistancePlotCardService : IResistancePlotCardService
{
    private readonly ResistanceActionGate _gate;
    private readonly IResistanceGamesRepository _games;

    public ResistancePlotCardService(ResistanceActionGate gate, IResistanceGamesRepository games)
    {
        _gate = gate;
        _games = games;
    }

    public Task<ErrorOr<ResistanceGameDto>> DealPlotCardsAsync(
        Guid sessionId, IReadOnlyList<ResistancePlotCardAssignmentDto>? assignments, GameCaller caller,
        CancellationToken cancellationToken)
    {
        return _gate.RunAsPlayerAsync(
            sessionId,
            caller,
            _ => _games.DealPlotCardsAsync(
                sessionId,
                caller.PlayerId,
                (assignments ?? []).Select(x => new PlotCardAssignment(x.CardId, x.PlayerId)).ToList(),
                cancellationToken),
            result => ResistanceErrors.From(result.Status),
            cancellationToken);
    }

    public Task<ErrorOr<ResistanceGameDto>> PlayPlotCardAsync(
        Guid sessionId, int cardId, Guid targetPlayerId, GameCaller caller, CancellationToken cancellationToken)
    {
        return RunAsync(
            sessionId,
            caller,
            () => _games.PlayPlotCardAsync(sessionId, caller.PlayerId, cardId, targetPlayerId, cancellationToken),
            cancellationToken);
    }

    public Task<ErrorOr<ResistanceGameDto>> ConfirmPlotCardAsync(
        Guid sessionId, int cardId, GameCaller caller, CancellationToken cancellationToken)
    {
        return RunAsync(
            sessionId,
            caller,
            () => _games.ConfirmPlotCardAsync(sessionId, caller.PlayerId, cardId, cancellationToken),
            cancellationToken);
    }

    public Task<ErrorOr<ResistanceGameDto>> TakeResponsibilityAsync(
        Guid sessionId, int cardId, Guid targetPlayerId, bool? takeOpinionMaker, GameCaller caller,
        CancellationToken cancellationToken)
    {
        return RunAsync(
            sessionId,
            caller,
            () => _games.TakeResponsibilityAsync(
                sessionId, caller.PlayerId, cardId, targetPlayerId, takeOpinionMaker, cancellationToken),
            cancellationToken);
    }

    public Task<ErrorOr<ResistanceGameDto>> TakeLeadAsync(
        Guid sessionId, int cardId, GameCaller caller, CancellationToken cancellationToken)
    {
        return RunAsync(
            sessionId,
            caller,
            () => _games.TakeLeadAsync(sessionId, caller.PlayerId, cardId, cancellationToken),
            cancellationToken);
    }

    public Task<ErrorOr<ResistanceGameDto>> PeekMissionCardAsync(
        Guid sessionId, int cardId, Guid targetPlayerId, GameCaller caller, CancellationToken cancellationToken)
    {
        return RunAsync(
            sessionId,
            caller,
            () => _games.PeekMissionCardAsync(sessionId, caller.PlayerId, cardId, targetPlayerId, cancellationToken),
            cancellationToken);
    }

    public Task<ErrorOr<ResistanceGameDto>> PlayNoConfidenceAsync(
        Guid sessionId, int cardId, GameCaller caller, CancellationToken cancellationToken)
    {
        return RunAsync(
            sessionId,
            caller,
            () => _games.PlayNoConfidenceAsync(sessionId, caller.PlayerId, cardId, cancellationToken),
            cancellationToken);
    }

    public Task<ErrorOr<ResistanceGameDto>> PassLeaderWindowAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        return RunAsync(
            sessionId,
            caller,
            () => _games.PassLeaderWindowAsync(sessionId, caller.PlayerId, cancellationToken),
            cancellationToken);
    }

    private Task<ErrorOr<ResistanceGameDto>> RunAsync(
        Guid sessionId,
        GameCaller caller,
        Func<Task<ResistancePlayCardResult>> call,
        CancellationToken cancellationToken)
    {
        return _gate.RunAsPlayerAsync(
            sessionId, caller, _ => call(), result => ResistanceErrors.From(result.Status), cancellationToken);
    }
}
