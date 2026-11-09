using ErrorOr;
using Microsoft.Extensions.Logging;
using PartyKing.Application.Mappings;
using PartyKing.Application.Playlists.Helpers;
using PartyKing.Application.System;
using PartyKing.Contract.V1.Enums;
using PartyKing.Contract.V1.Playlists;
using PartyKing.Domain.Entities.Playlists;
using PartyKing.Domain.Repositories;

namespace PartyKing.Application.Playlists.Services;

public interface ISavedPlaylistService
{
    /// <summary>
    /// Saves the chosen parts of the session's queue as a new playlist of the account. The songs are read here, never
    /// taken from the caller.
    /// </summary>
    Task<ErrorOr<SavedPlaylistDto>> SaveQueueAsync(
        Guid accountId,
        Guid sessionId,
        string? name,
        IReadOnlyCollection<PlaylistSection> sections,
        CancellationToken cancellationToken);

    /// <summary>How many songs every part of the session's queue holds now.</summary>
    Task<ErrorOr<SavedPlaylistPreviewDto>> GetPreviewAsync(Guid sessionId, CancellationToken cancellationToken);
}

internal class SavedPlaylistService : ISavedPlaylistService
{
    private readonly ISavedPlaylistsRepository _savedPlaylistsRepository;
    private readonly IQueueRepository _queueRepository;
    private readonly IQueueSelection _queueSelection;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<SavedPlaylistService> _logger;

    public SavedPlaylistService(
        ISavedPlaylistsRepository savedPlaylistsRepository,
        IQueueRepository queueRepository,
        IQueueSelection queueSelection,
        IDateTimeProvider dateTimeProvider,
        ILogger<SavedPlaylistService> logger)
    {
        _savedPlaylistsRepository = savedPlaylistsRepository;
        _queueRepository = queueRepository;
        _queueSelection = queueSelection;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<ErrorOr<SavedPlaylistDto>> SaveQueueAsync(
        Guid accountId,
        Guid sessionId,
        string? name,
        IReadOnlyCollection<PlaylistSection> sections,
        CancellationToken cancellationToken)
    {
        var itemsTask = _queueRepository.GetAllAsync(sessionId, cancellationToken);
        var ownerPlaylistCountTask = _savedPlaylistsRepository.CountByOwnerAsync(accountId, cancellationToken);

        await Task.WhenAll(itemsTask, ownerPlaylistCountTask);

        var songs = _queueSelection.Select(itemsTask.Result, sections.Select(x => x.ToDomain()).ToList());
        var ownerPlaylistCount = ownerPlaylistCountTask.Result;

        var creation = SavedPlaylist.Create(
            Guid.NewGuid(),
            accountId,
            name,
            songs,
            ownerPlaylistCount,
            _dateTimeProvider.UtcNow.UtcDateTime);

        if (creation.Playlist is null)
        {
            return SavedPlaylistErrors.From(creation.Failure);
        }

        await _savedPlaylistsRepository.AddAsync(creation.Playlist, cancellationToken);

        _logger.LogInformation(
            "Account {AccountId} saved {SongCount} songs of session {SessionId} as playlist {PlaylistId}",
            accountId,
            creation.Playlist.Items.Count,
            sessionId,
            creation.Playlist.Id);

        return creation.Playlist.ToDto();
    }

    public async Task<ErrorOr<SavedPlaylistPreviewDto>> GetPreviewAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var items = await _queueRepository.GetAllAsync(sessionId, cancellationToken);

        return new SavedPlaylistPreviewDto(_queueSelection.Count(items).Select(x => x.ToDto()).ToList());
    }
}
