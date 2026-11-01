using PartyKing.Application.Events;
using PartyKing.Application.Games.Services.Base;
using PartyKing.Application.Realtime;
using PartyKing.Domain.Entities.Games.Base;
using PartyKing.Domain.Events.Games.Base;

namespace PartyKing.Application.Games.Handlers;

/// <summary>
/// Tells the session's clients that a lobby changed, whichever game it belongs to: the payload-free push each game's
/// notifier sends. Opening also sends the "opened" push that asks guests whether to join.
/// </summary>
internal sealed class LobbyPushHandler :
    IDomainEventHandler<LobbyOpened>,
    IDomainEventHandler<LobbyPlayerJoined>,
    IDomainEventHandler<LobbyPlayerKicked>,
    IDomainEventHandler<LobbyOrderChanged>,
    IDomainEventHandler<LobbyGameEnded>
{
    private readonly IRingoNotifier _ringo;
    private readonly IResistanceNotifier _resistance;

    public LobbyPushHandler(IRingoNotifier ringo, IResistanceNotifier resistance)
    {
        _ringo = ringo;
        _resistance = resistance;
    }

    public async Task HandleAsync(LobbyOpened domainEvent, CancellationToken cancellationToken)
    {
        var notifier = NotifierOf(domainEvent.Kind);
        await notifier.OpenedAsync(domainEvent.SessionId, cancellationToken);
        await notifier.ChangedAsync(domainEvent.SessionId, cancellationToken);
    }

    public Task HandleAsync(LobbyPlayerJoined domainEvent, CancellationToken cancellationToken)
    {
        return NotifierOf(domainEvent.Kind).ChangedAsync(domainEvent.SessionId, cancellationToken);
    }

    public Task HandleAsync(LobbyPlayerKicked domainEvent, CancellationToken cancellationToken)
    {
        return NotifierOf(domainEvent.Kind).ChangedAsync(domainEvent.SessionId, cancellationToken);
    }

    public Task HandleAsync(LobbyGameEnded domainEvent, CancellationToken cancellationToken)
    {
        return NotifierOf(domainEvent.Kind).ChangedAsync(domainEvent.SessionId, cancellationToken);
    }

    public Task HandleAsync(LobbyOrderChanged domainEvent, CancellationToken cancellationToken)
    {
        return NotifierOf(domainEvent.Kind).ChangedAsync(domainEvent.SessionId, cancellationToken);
    }

    private IGameLobbyNotifier NotifierOf(GameKind kind)
    {
        return kind switch
        {
            GameKind.Ringo => _ringo,
            GameKind.Resistance => _resistance,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }
}
