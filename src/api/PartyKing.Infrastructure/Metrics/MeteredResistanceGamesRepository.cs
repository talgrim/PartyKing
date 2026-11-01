using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Infrastructure.Results;

namespace PartyKing.Infrastructure.Metrics;

internal sealed class MeteredResistanceGamesRepository
    : MeteredLobbyGamesRepository<ResistanceGame, ResistancePlayer>, IResistanceGamesRepository
{
    private readonly IResistanceGamesRepository _inner;

    public MeteredResistanceGamesRepository(IResistanceGamesRepository inner, RepositoryMetrics metrics)
        : base(nameof(ResistanceGamesRepository), inner, metrics)
    {
        _inner = inner;
    }

    public Task<ResistancePlayCardResult> PlayPlotCardAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, Guid targetPlayerId, CancellationToken cancellationToken) =>
        MeasureAsync(
            nameof(PlayPlotCardAsync),
            () => _inner.PlayPlotCardAsync(sessionId, callerPlayerId, cardId, targetPlayerId, cancellationToken));

    public Task<ResistancePlayCardResult> TakeResponsibilityAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, Guid targetPlayerId, bool? takeOpinionMaker,
        CancellationToken cancellationToken) =>
        MeasureAsync(
            nameof(TakeResponsibilityAsync),
            () => _inner.TakeResponsibilityAsync(
                sessionId, callerPlayerId, cardId, targetPlayerId, takeOpinionMaker, cancellationToken));

    public Task<ResistancePlayCardResult> TakeLeadAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, CancellationToken cancellationToken) =>
        MeasureAsync(nameof(TakeLeadAsync), () => _inner.TakeLeadAsync(sessionId, callerPlayerId, cardId, cancellationToken));

    public Task<ResistancePlayCardResult> PeekMissionCardAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, Guid targetPlayerId, CancellationToken cancellationToken) =>
        MeasureAsync(nameof(PeekMissionCardAsync), () => _inner.PeekMissionCardAsync(sessionId, callerPlayerId, cardId, targetPlayerId, cancellationToken));

    public Task<ResistancePlayCardResult> PlayNoConfidenceAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, CancellationToken cancellationToken) =>
        MeasureAsync(nameof(PlayNoConfidenceAsync), () => _inner.PlayNoConfidenceAsync(sessionId, callerPlayerId, cardId, cancellationToken));

    public Task<ResistancePlayCardResult> PassLeaderWindowAsync(
        Guid sessionId, Guid? callerPlayerId, CancellationToken cancellationToken) =>
        MeasureAsync(
            nameof(PassLeaderWindowAsync), () => _inner.PassLeaderWindowAsync(sessionId, callerPlayerId, cancellationToken));

    public Task<ResistancePlayCardResult> ConfirmPlotCardAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, CancellationToken cancellationToken) =>
        MeasureAsync(
            nameof(ConfirmPlotCardAsync),
            () => _inner.ConfirmPlotCardAsync(sessionId, callerPlayerId, cardId, cancellationToken));

    public Task<ResistanceDealResult> DealPlotCardsAsync(
        Guid sessionId, Guid? callerPlayerId, IReadOnlyList<PlotCardAssignment> assignments,
        CancellationToken cancellationToken)
    {
        return MeasureAsync(
            nameof(DealPlotCardsAsync),
            () => _inner.DealPlotCardsAsync(sessionId, callerPlayerId, assignments, cancellationToken));
    }

    public Task<ResistanceMissionCardResult> PlayMissionCardAsync(
        Guid sessionId, Guid? callerPlayerId, bool success, CancellationToken cancellationToken)
    {
        return MeasureAsync(
            nameof(PlayMissionCardAsync),
            () => _inner.PlayMissionCardAsync(sessionId, callerPlayerId, success, cancellationToken));
    }

    public Task<ResistanceContinueResult> ContinueAsync(
        Guid sessionId, Guid? callerPlayerId, bool isManager, CancellationToken cancellationToken)
    {
        return MeasureAsync(
            nameof(ContinueAsync),
            () => _inner.ContinueAsync(sessionId, callerPlayerId, isManager, cancellationToken));
    }
}
