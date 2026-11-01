using PartyKing.Domain.Repositories;
using ErrorOr;
using Microsoft.Extensions.Logging;
using PartyKing.Application.Mappings;
using PartyKing.Application.Queue.Services;
using PartyKing.Application.Realtime;
using PartyKing.Application.Sessions.Helpers;
using PartyKing.Contract.V1.Enums;
using PartyKing.Contract.V1.Player;
using PartyKing.Contract.V1.Queue;

namespace PartyKing.Application.Player.Services;

public interface IPlayerService
{
    Task<ErrorOr<NowPlayingDto>> GetNowPlayingAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Host only: forbidden unless <paramref name="hostProfileId"/> hosts the session. Returns the command an admin
    /// sent in the meantime, if any.
    /// </summary>
    Task<ErrorOr<PlayerStateResponseDto>> ReportStateAsync(
        Guid sessionId,
        string hostProfileId,
        PlayerStateRequestDto state,
        CancellationToken cancellationToken);

    /// <summary>Admin: asks the host's player to pause or resume. Conflict when no player is connected.</summary>
    Task<ErrorOr<Success>> SendCommandAsync(
        Guid sessionId,
        PlayerCommand command,
        CancellationToken cancellationToken);

    /// <summary>
    /// Admin: skips to the next song, which the host's player then picks up. Conflict when no player is connected;
    /// <paramref name="currentSongId"/> is the song the admin saw playing.
    /// </summary>
    Task<ErrorOr<QueueItemDto>> SkipAsync(Guid sessionId, int? currentSongId, CancellationToken cancellationToken);
}

internal class PlayerService : IPlayerService
{
    private readonly ISessionsRepository _sessionsRepository;
    private readonly IQueueRepository _queueRepository;
    private readonly IPlayerStateStore _playerStateStore;
    private readonly ISessionMusicNotifier _musicNotifier;
    private readonly IPlayerConnectionWatchdog _connectionWatchdog;
    private readonly IFallbackQueueFiller _fallbackFiller;
    private readonly IFallbackStatusStore _fallbackStatusStore;
    private readonly ILogger<PlayerService> _logger;

    public PlayerService(
        ISessionsRepository sessionsRepository,
        IQueueRepository queueRepository,
        IPlayerStateStore playerStateStore,
        ISessionMusicNotifier musicNotifier,
        IPlayerConnectionWatchdog connectionWatchdog,
        IFallbackQueueFiller fallbackFiller,
        IFallbackStatusStore fallbackStatusStore,
        ILogger<PlayerService> logger)
    {
        _sessionsRepository = sessionsRepository;
        _queueRepository = queueRepository;
        _playerStateStore = playerStateStore;
        _musicNotifier = musicNotifier;
        _connectionWatchdog = connectionWatchdog;
        _fallbackFiller = fallbackFiller;
        _fallbackStatusStore = fallbackStatusStore;
        _logger = logger;
    }

    public async Task<ErrorOr<NowPlayingDto>> GetNowPlayingAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (await _sessionsRepository.GetAsync(sessionId, cancellationToken) is null)
        {
            return SessionErrors.NotFound();
        }

        var playing = await _queueRepository.GetPlayingAsync(sessionId, cancellationToken);
        var player = _playerStateStore.Get(sessionId);

        // A pause belongs to the song it was reported for, so it can't leak into the next one
        var paused = playing is not null && player.SongId == playing.Id && player.Paused;

        var positionMs = playing is not null && player.SongId == playing.Id ? player.PositionMs : 0;

        return new NowPlayingDto(playing?.ToDto(), paused, player.Connected, positionMs);
    }

    public async Task<ErrorOr<PlayerStateResponseDto>> ReportStateAsync(
        Guid sessionId,
        string hostProfileId,
        PlayerStateRequestDto state,
        CancellationToken cancellationToken)
    {
        var session = await _sessionsRepository.GetAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return SessionErrors.NotFound();
        }

        if (session.HostProfileId != hostProfileId)
        {
            return Error.Forbidden(description: "Only the host of this session can report its player state");
        }

        var positionMs = Math.Max(0, state.PositionMs);
        var report = _playerStateStore.Report(sessionId, state.SongId, state.Paused, positionMs, state.Seeked);
        _connectionWatchdog.Arm(sessionId, state.SongId, state.Paused);

        // A heartbeat that changes nothing stays between the host and the server
        if (report.Changed)
        {
            await _musicNotifier.PlayerStateChangedAsync(
                sessionId,
                new PlayerStateChangedDto(state.SongId, state.Paused, PlayerConnected: true, positionMs),
                cancellationToken);
        }

        return new PlayerStateResponseDto(report.Command, _fallbackStatusStore.GetError(sessionId));
    }

    public async Task<ErrorOr<Success>> SendCommandAsync(
        Guid sessionId,
        PlayerCommand command,
        CancellationToken cancellationToken)
    {
        if (await _sessionsRepository.GetAsync(sessionId, cancellationToken) is null)
        {
            return SessionErrors.NotFound();
        }

        if (!_playerStateStore.TrySendCommand(sessionId, command))
        {
            return NoPlayerConnected();
        }

        _logger.LogInformation("Sent the {Command} command to the player of session {SessionId}", command, sessionId);

        // The next report would hand the command over too, but that can be ~10 s away
        await _musicNotifier.PlayerCommandAsync(sessionId, new PlayerCommandDto(command), cancellationToken);

        return Result.Success;
    }

    public async Task<ErrorOr<QueueItemDto>> SkipAsync(
        Guid sessionId,
        int? currentSongId,
        CancellationToken cancellationToken)
    {
        if (await _sessionsRepository.GetAsync(sessionId, cancellationToken) is null)
        {
            return SessionErrors.NotFound();
        }

        if (!_playerStateStore.Get(sessionId).Connected)
        {
            return NoPlayerConnected();
        }

        await _fallbackFiller.EnsureQueuedAsync(sessionId, cancellationToken);

        var playingBefore = await _queueRepository.GetPlayingAsync(sessionId, cancellationToken);
        var next = await _queueRepository.AdvanceAsync(sessionId, currentSongId, cancellationToken);

        await _fallbackFiller.EnsureQueuedAsync(sessionId, cancellationToken);

        if (playingBefore?.Id == currentSongId)
        {
            _logger.LogInformation(
                "An admin skipped session {SessionId} from song {PreviousSongId} to {NextSongId}",
                sessionId,
                playingBefore?.Id,
                next?.Id);
            await _musicNotifier.QueueAdvancedAsync(
                sessionId,
                playingBefore?.Id,
                next?.ToDto(),
                cancellationToken);
        }

        return next is null
            ? Error.NotFound(description: "The queue is empty")
            : next.ToDto();
    }


    private static Error NoPlayerConnected() =>
        Error.Conflict(description: "The host's player is not connected to this session");
}
