using PartyKing.Contract.V1.Player;

namespace PartyKing.Application.Realtime;

/// <summary>Pushes the host player's state, admin commands and the fallback playlist status to a session.</summary>
public interface IPlayerNotifier
{
    Task PlayerStateChangedAsync(Guid sessionId, PlayerStateChangedDto state, CancellationToken cancellationToken);

    Task PlayerCommandAsync(Guid sessionId, PlayerCommandDto command, CancellationToken cancellationToken);

    Task FallbackErrorChangedAsync(Guid sessionId, FallbackErrorDto error, CancellationToken cancellationToken);
}
