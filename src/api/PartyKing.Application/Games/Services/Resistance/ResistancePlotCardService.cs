using ErrorOr;
using PartyKing.Contract.V1.Games.Resistance;
using PartyKing.Application.System;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Repositories;

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
    private readonly IDateTimeProvider _clock;

    public ResistancePlotCardService(
        ResistanceActionGate gate, IResistanceGamesRepository games, IDateTimeProvider clock)
    {
        _gate = gate;
        _games = games;
        _clock = clock;
    }

    public Task<ErrorOr<ResistanceGameDto>> DealPlotCardsAsync(
        Guid sessionId, IReadOnlyList<ResistancePlotCardAssignmentDto>? assignments, GameCaller caller,
        CancellationToken cancellationToken)
    {
        var deal = (assignments ?? []).Select(x => new PlotCardAssignment(x.CardId, x.PlayerId)).ToList();

        return RunAsync(
            sessionId,
            caller,
            nameof(DealPlotCardsAsync),
            game => game.DealPlotCards(caller.PlayerId, deal),
            cancellationToken);
    }

    public Task<ErrorOr<ResistanceGameDto>> PlayPlotCardAsync(
        Guid sessionId, int cardId, Guid targetPlayerId, GameCaller caller, CancellationToken cancellationToken)
    {
        return RunAsync(
            sessionId,
            caller,
            nameof(PlayPlotCardAsync),
            game => game.PlayPlotCard(caller.PlayerId, cardId, targetPlayerId, _clock.UtcNow),
            cancellationToken);
    }

    public Task<ErrorOr<ResistanceGameDto>> ConfirmPlotCardAsync(
        Guid sessionId, int cardId, GameCaller caller, CancellationToken cancellationToken)
    {
        return RunAsync(
            sessionId,
            caller,
            nameof(ConfirmPlotCardAsync),
            game => game.ConfirmPlotCard(caller.PlayerId, cardId),
            cancellationToken);
    }

    public Task<ErrorOr<ResistanceGameDto>> TakeResponsibilityAsync(
        Guid sessionId, int cardId, Guid targetPlayerId, bool? takeOpinionMaker, GameCaller caller,
        CancellationToken cancellationToken)
    {
        return RunAsync(
            sessionId,
            caller,
            nameof(TakeResponsibilityAsync),
            game => game.TakeResponsibility(
                caller.PlayerId, cardId, targetPlayerId, takeOpinionMaker, _clock.UtcNow, Random.Shared),
            cancellationToken);
    }

    public Task<ErrorOr<ResistanceGameDto>> TakeLeadAsync(
        Guid sessionId, int cardId, GameCaller caller, CancellationToken cancellationToken)
    {
        return RunAsync(
            sessionId,
            caller,
            nameof(TakeLeadAsync),
            game => game.TakeLead(caller.PlayerId, cardId, _clock.UtcNow),
            cancellationToken);
    }

    public Task<ErrorOr<ResistanceGameDto>> PeekMissionCardAsync(
        Guid sessionId, int cardId, Guid targetPlayerId, GameCaller caller, CancellationToken cancellationToken)
    {
        return RunAsync(
            sessionId,
            caller,
            nameof(PeekMissionCardAsync),
            game => game.PeekMissionCard(caller.PlayerId, cardId, targetPlayerId, _clock.UtcNow),
            cancellationToken);
    }

    public Task<ErrorOr<ResistanceGameDto>> PlayNoConfidenceAsync(
        Guid sessionId, int cardId, GameCaller caller, CancellationToken cancellationToken)
    {
        return RunAsync(
            sessionId,
            caller,
            nameof(PlayNoConfidenceAsync),
            game => game.PlayNoConfidence(caller.PlayerId, cardId, _clock.UtcNow),
            cancellationToken);
    }

    public Task<ErrorOr<ResistanceGameDto>> PassLeaderWindowAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        return RunAsync(
            sessionId,
            caller,
            nameof(PassLeaderWindowAsync),
            game => game.PassLeaderWindow(caller.PlayerId),
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
