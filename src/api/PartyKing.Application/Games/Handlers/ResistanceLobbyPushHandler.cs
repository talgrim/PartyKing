using PartyKing.Application.Events;
using PartyKing.Application.Realtime;
using PartyKing.Domain.Entities.Games.Base;
using PartyKing.Domain.Events.Games.Base;
using PartyKing.Domain.Events.Games.Resistance;

namespace PartyKing.Application.Games.Handlers;

/// <summary>
/// Tells the clients that what the Resistance lobby shows changed: the game was created, opened, started, restarted
/// or ended, or a player joined or was kicked, or a seat was released or taken over (the seats a guest may take over
/// are part of what the join page of a started game shows). The lobby (exists, open, started, joined) does not change with the
/// actions of a round, so those pushes (<c>ResistanceChanged</c>) leave it alone and a client that only shows the
/// invite is not woken by every vote. Reordering changes no lobby field. The lobby events of Ringo are ignored.
/// </summary>
internal sealed class ResistanceLobbyPushHandler :
    IDomainEventHandler<ResistanceCreated>,
    IDomainEventHandler<ResistanceStarted>,
    IDomainEventHandler<ResistanceRestarted>,
    IDomainEventHandler<LobbyOpened>,
    IDomainEventHandler<LobbyPlayerJoined>,
    IDomainEventHandler<LobbyPlayerKicked>,
    IDomainEventHandler<LobbyGameEnded>,
    IDomainEventHandler<ResistanceSeatReleaseChanged>,
    IDomainEventHandler<ResistanceSeatClaimed>
{
    private readonly IResistanceNotifier _notifier;

    public ResistanceLobbyPushHandler(IResistanceNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task HandleAsync(ResistanceCreated domainEvent, CancellationToken cancellationToken)
    {
        return _notifier.LobbyChangedAsync(domainEvent.SessionId, cancellationToken);
    }

    public Task HandleAsync(ResistanceStarted domainEvent, CancellationToken cancellationToken)
    {
        return _notifier.LobbyChangedAsync(domainEvent.SessionId, cancellationToken);
    }

    public Task HandleAsync(ResistanceRestarted domainEvent, CancellationToken cancellationToken)
    {
        return _notifier.LobbyChangedAsync(domainEvent.SessionId, cancellationToken);
    }

    public Task HandleAsync(LobbyOpened domainEvent, CancellationToken cancellationToken)
    {
        return PushIfResistanceAsync(domainEvent.Kind, domainEvent.SessionId, cancellationToken);
    }

    public Task HandleAsync(LobbyPlayerJoined domainEvent, CancellationToken cancellationToken)
    {
        return PushIfResistanceAsync(domainEvent.Kind, domainEvent.SessionId, cancellationToken);
    }

    public Task HandleAsync(LobbyPlayerKicked domainEvent, CancellationToken cancellationToken)
    {
        return PushIfResistanceAsync(domainEvent.Kind, domainEvent.SessionId, cancellationToken);
    }

    public Task HandleAsync(LobbyGameEnded domainEvent, CancellationToken cancellationToken)
    {
        return PushIfResistanceAsync(domainEvent.Kind, domainEvent.SessionId, cancellationToken);
    }

    public Task HandleAsync(ResistanceSeatReleaseChanged domainEvent, CancellationToken cancellationToken)
    {
        return _notifier.LobbyChangedAsync(domainEvent.SessionId, cancellationToken);
    }

    public Task HandleAsync(ResistanceSeatClaimed domainEvent, CancellationToken cancellationToken)
    {
        return _notifier.LobbyChangedAsync(domainEvent.SessionId, cancellationToken);
    }

    private Task PushIfResistanceAsync(GameKind kind, Guid sessionId, CancellationToken cancellationToken)
    {
        return kind == GameKind.Resistance
            ? _notifier.LobbyChangedAsync(sessionId, cancellationToken)
            : Task.CompletedTask;
    }
}
