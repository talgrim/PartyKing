using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Repositories;

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

    public Task<ResistanceRulesResult> RefreshRulesAsync(
        Guid sessionId, ResistanceRules rules, CancellationToken cancellationToken)
    {
        return MeasureAsync(
            nameof(RefreshRulesAsync), () => _inner.RefreshRulesAsync(sessionId, rules, cancellationToken));
    }

    public Task<ResistanceRulesResult> SetPlotCardsAsync(
        Guid sessionId, bool enabled, CancellationToken cancellationToken)
    {
        return MeasureAsync(
            nameof(SetPlotCardsAsync), () => _inner.SetPlotCardsAsync(sessionId, enabled, cancellationToken));
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

    public Task<ResistanceStartResult> StartAsync(
        Guid sessionId, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(StartAsync), () => _inner.StartAsync(sessionId, startedAt, cancellationToken));
    }

    public Task<ResistanceRestartResult> RestartAsync(
        Guid sessionId, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(RestartAsync), () => _inner.RestartAsync(sessionId, startedAt, cancellationToken));
    }

    public Task<ResistanceTeamResult> SelectTeamAsync(
        Guid sessionId, Guid? callerPlayerId, IReadOnlyList<Guid> playerIds, CancellationToken cancellationToken)
    {
        return MeasureAsync(
            nameof(SelectTeamAsync),
            () => _inner.SelectTeamAsync(sessionId, callerPlayerId, playerIds, cancellationToken));
    }

    public Task<ResistanceVoteResult> VoteAsync(
        Guid sessionId, Guid? callerPlayerId, bool approve, CancellationToken cancellationToken)
    {
        return MeasureAsync(
            nameof(VoteAsync), () => _inner.VoteAsync(sessionId, callerPlayerId, approve, cancellationToken));
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
