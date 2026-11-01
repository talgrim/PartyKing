using Microsoft.AspNetCore.SignalR;
using PartyKing.Application.Realtime;
using PartyKing.Contract.V1.Player;

namespace PartyKing.API.Hubs.Notifiers;

internal sealed class SignalRPlayerNotifier : SessionGroupNotifier, IPlayerNotifier
{
    public SignalRPlayerNotifier(IHubContext<SessionHub> hub) : base(hub)
    {
    }

    public Task PlayerStateChangedAsync(
        Guid sessionId,
        PlayerStateChangedDto state,
        CancellationToken cancellationToken)
    {
        return Clients(sessionId).SendAsync(SessionMusicHubEvents.PlayerStateChanged, state, cancellationToken);
    }

    public Task PlayerCommandAsync(Guid sessionId, PlayerCommandDto command, CancellationToken cancellationToken)
    {
        return Clients(sessionId).SendAsync(SessionMusicHubEvents.PlayerCommand, command, cancellationToken);
    }

    public Task FallbackErrorChangedAsync(
        Guid sessionId,
        FallbackErrorDto error,
        CancellationToken cancellationToken)
    {
        return Clients(sessionId).SendAsync(SessionMusicHubEvents.FallbackErrorChanged, error, cancellationToken);
    }
}
