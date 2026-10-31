using PartyKing.Application.Realtime;

namespace PartyKing.Application.Games.Services.Ringo;

/// <summary>Tells a session's clients that its Ringo lobby opened or changed.</summary>
internal sealed class RingoLobbyNotifier : IGameLobbyNotifier
{
    private readonly ISessionMusicNotifier _notifier;

    public RingoLobbyNotifier(ISessionMusicNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task OpenedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return _notifier.RingoOpenedAsync(sessionId, cancellationToken);
    }

    public Task ChangedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return _notifier.RingoChangedAsync(sessionId, cancellationToken);
    }
}
