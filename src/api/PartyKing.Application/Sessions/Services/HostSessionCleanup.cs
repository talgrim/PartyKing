using PartyKing.Application.Player.Services;
using PartyKing.Application.Realtime;
using PartyKing.Application.Spotify.Services;
using PartyKing.Contract.V1.Player;

namespace PartyKing.Application.Sessions.Services;

public interface IHostSessionCleanup
{
    /// <summary>Drops what the server remembers about the session's player and its host's Spotify token.</summary>
    Task ForgetHostAsync(Guid sessionId, string? hostProfileId, CancellationToken cancellationToken);
}

internal class HostSessionCleanup : IHostSessionCleanup
{
    private readonly IPlayerStateStore _playerStateStore;
    private readonly IHostTokenProvider _hostTokenProvider;
    private readonly ISessionMusicNotifier _musicNotifier;
    private readonly IPlayerConnectionWatchdog _connectionWatchdog;

    public HostSessionCleanup(
        IPlayerStateStore playerStateStore,
        IHostTokenProvider hostTokenProvider,
        ISessionMusicNotifier musicNotifier,
        IPlayerConnectionWatchdog connectionWatchdog)
    {
        _playerStateStore = playerStateStore;
        _hostTokenProvider = hostTokenProvider;
        _musicNotifier = musicNotifier;
        _connectionWatchdog = connectionWatchdog;
    }

    public async Task ForgetHostAsync(Guid sessionId, string? hostProfileId, CancellationToken cancellationToken)
    {
        _playerStateStore.Remove(sessionId);
        _connectionWatchdog.Disarm(sessionId);
        await _musicNotifier.PlayerStateChangedAsync(
            sessionId,
            new PlayerStateChangedDto(SongId: null, Paused: false, PlayerConnected: false),
            cancellationToken);

        // The account may host another session by now, but then it just refreshes its token again
        if (hostProfileId is not null)
        {
            _hostTokenProvider.Invalidate(hostProfileId);
        }
    }
}
