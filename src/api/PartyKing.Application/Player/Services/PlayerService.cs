using ErrorOr;
using PartyKing.Application.Mappings;
using PartyKing.Contract.V1.Enums;
using PartyKing.Contract.V1.Player;
using PartyKing.Contract.V1.Queue;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Application.Player.Services;

public interface IPlayerService
{
    Task<ErrorOr<NowPlayingDto>> GetNowPlayingAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Host only: forbidden unless <paramref name="hostProfileId"/> hosts the session. Returns the command an admin
    /// sent in the meantime, if any.
    /// </summary>
    Task<ErrorOr<PlayerStateResponse>> ReportStateAsync(
        Guid sessionId,
        string hostProfileId,
        PlayerStateRequest state,
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
    Task<ErrorOr<SongDataDto>> SkipAsync(Guid sessionId, int? currentSongId, CancellationToken cancellationToken);
}

internal class PlayerService : IPlayerService
{
    private readonly ISessionsRepository _sessionsRepository;
    private readonly IQueueRepository _queueRepository;
    private readonly IPlayerStateStore _playerStateStore;

    public PlayerService(
        ISessionsRepository sessionsRepository,
        IQueueRepository queueRepository,
        IPlayerStateStore playerStateStore)
    {
        _sessionsRepository = sessionsRepository;
        _queueRepository = queueRepository;
        _playerStateStore = playerStateStore;
    }

    public async Task<ErrorOr<NowPlayingDto>> GetNowPlayingAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (await _sessionsRepository.GetAsync(sessionId, cancellationToken) is null)
        {
            return SessionNotFound();
        }

        var playing = await _queueRepository.GetPlayingAsync(sessionId, cancellationToken);
        var player = _playerStateStore.Get(sessionId);

        // A pause belongs to the song it was reported for, so it can't leak into the next one
        var paused = playing is not null && player.SongId == playing.Id && player.Paused;

        return new NowPlayingDto(playing?.ToDto(), paused, player.Connected);
    }

    public async Task<ErrorOr<PlayerStateResponse>> ReportStateAsync(
        Guid sessionId,
        string hostProfileId,
        PlayerStateRequest state,
        CancellationToken cancellationToken)
    {
        var session = await _sessionsRepository.GetAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return SessionNotFound();
        }

        if (session.HostProfileId != hostProfileId)
        {
            return Error.Forbidden(description: "Only the host of this session can report its player state");
        }

        var command = _playerStateStore.Report(sessionId, state.SongId, state.Paused);

        return new PlayerStateResponse(command);
    }

    public async Task<ErrorOr<Success>> SendCommandAsync(
        Guid sessionId,
        PlayerCommand command,
        CancellationToken cancellationToken)
    {
        if (await _sessionsRepository.GetAsync(sessionId, cancellationToken) is null)
        {
            return SessionNotFound();
        }

        return _playerStateStore.TrySendCommand(sessionId, command) ? Result.Success : NoPlayerConnected();
    }

    public async Task<ErrorOr<SongDataDto>> SkipAsync(
        Guid sessionId,
        int? currentSongId,
        CancellationToken cancellationToken)
    {
        if (await _sessionsRepository.GetAsync(sessionId, cancellationToken) is null)
        {
            return SessionNotFound();
        }

        if (!_playerStateStore.Get(sessionId).Connected)
        {
            return NoPlayerConnected();
        }

        var next = await _queueRepository.AdvanceAsync(sessionId, currentSongId, cancellationToken);

        return next is null ? Error.NotFound(description: "The queue is empty") : next.ToDto();
    }

    private static Error SessionNotFound() => Error.NotFound(description: "The session was not found");

    private static Error NoPlayerConnected() =>
        Error.Conflict(description: "The host's player is not connected to this session");
}
