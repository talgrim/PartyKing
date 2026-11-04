using PartyKing.Application.Events;
using PartyKing.Application.Realtime;
using PartyKing.Domain.Entities.Games.Base;
using PartyKing.Domain.Events.Games.Resistance;

namespace PartyKing.Application.Games.Handlers;

/// <summary>
/// A seat was taken over, so the connections of the device that lost it stop counting: the old tab can still be open
/// and connected, and would keep the seat online (and so out of anyone's reach) after it lost its right to it.
/// </summary>
internal sealed class ResistanceSeatClaimedHandler : IDomainEventHandler<ResistanceSeatClaimed>
{
    private readonly IGamePresence _presence;

    public ResistanceSeatClaimedHandler(IGamePresence presence)
    {
        _presence = presence;
    }

    public Task HandleAsync(ResistanceSeatClaimed domainEvent, CancellationToken cancellationToken)
    {
        return _presence.ForgetAsync(GameKind.Resistance, domainEvent.SessionId, domainEvent.PlayerId);
    }
}
