using Microsoft.AspNetCore.SignalR;
using PartyKing.Application.Realtime;
using PartyKing.Contract.V1.Lyrics;

namespace PartyKing.API.Hubs.Notifiers;

internal sealed class SignalRLyricsNotifier : SessionGroupNotifier, ILyricsNotifier
{
    public SignalRLyricsNotifier(IHubContext<SessionHub> hub) : base(hub)
    {
    }

    public Task LyricsOffsetChangedAsync(
        Guid sessionId,
        LyricsOffsetChangedDto offset,
        CancellationToken cancellationToken)
    {
        return Clients(sessionId).SendAsync(SessionMusicHubEvents.LyricsOffsetChanged, offset, cancellationToken);
    }
}
