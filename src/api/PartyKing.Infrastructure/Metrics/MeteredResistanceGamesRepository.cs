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

    public Task<ResistanceStartResult> StartAsync(
        Guid sessionId, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(StartAsync), () => _inner.StartAsync(sessionId, startedAt, cancellationToken));
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
