using PartyKing.Application.Realtime;

namespace PartyKing.Application.Games.Services.Resistance;

/// <summary>Tells a session's clients that its Resistance lobby opened or changed.</summary>
internal sealed class ResistanceLobbyNotifier : IGameLobbyNotifier
{
    private readonly ISessionMusicNotifier _notifier;

    public ResistanceLobbyNotifier(ISessionMusicNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task OpenedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return _notifier.ResistanceOpenedAsync(sessionId, cancellationToken);
    }

    public Task ChangedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return _notifier.ResistanceChangedAsync(sessionId, cancellationToken);
    }
}
