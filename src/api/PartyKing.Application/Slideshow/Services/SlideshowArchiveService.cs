using ErrorOr;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Ports;
using PartyKing.Application.Sessions;
using PartyKing.Application.Sessions.Helpers;
using PartyKing.Application.Sessions.Services;
using PartyKing.Contract.V1.Slideshow;
using PartyKing.Domain.Repositories;

namespace PartyKing.Application.Slideshow.Services;

/// <summary>A zip of a session's photos on disk; whoever receives it deletes the file.</summary>
public sealed record SlideshowArchive(string FilePath, string DownloadName);

/// <summary>Lets the host or an admin download everything that was uploaded to a session.</summary>
public interface ISlideshowArchiveService
{
    Task<ErrorOr<SlideshowPhotoCountDto>> CountPhotosAsync(
        Guid sessionId,
        SessionCaller caller,
        CancellationToken cancellationToken);

    /// <summary>The photos still on disk as a zip; NotFound when the session has none.</summary>
    Task<ErrorOr<SlideshowArchive>> CreateArchiveAsync(
        Guid sessionId,
        SessionCaller caller,
        CancellationToken cancellationToken);
}

internal sealed class SlideshowArchiveService : ISlideshowArchiveService
{
    private readonly ISessionAuthorizer _authorizer;
    private readonly ISessionsRepository _sessionsRepository;
    private readonly ISlideshowImagesRepository _imagesRepository;
    private readonly SlideshowConfiguration _configuration;

    public SlideshowArchiveService(
        ISessionAuthorizer authorizer,
        ISessionsRepository sessionsRepository,
        ISlideshowImagesRepository imagesRepository,
        IOptions<SlideshowConfiguration> options)
    {
        _authorizer = authorizer;
        _sessionsRepository = sessionsRepository;
        _imagesRepository = imagesRepository;
        _configuration = options.Value;
    }

    public Task<ErrorOr<SlideshowPhotoCountDto>> CountPhotosAsync(
        Guid sessionId,
        SessionCaller caller,
        CancellationToken cancellationToken)
    {
        return _authorizer.RunAsManagerAsync(
            sessionId,
            caller,
            () => Task.FromResult<ErrorOr<SlideshowPhotoCountDto>>(new SlideshowPhotoCountDto
            {
                Count = _imagesRepository.CountSessionPhotos(SessionDirectory(sessionId))
            }),
            cancellationToken);
    }

    public Task<ErrorOr<SlideshowArchive>> CreateArchiveAsync(
        Guid sessionId,
        SessionCaller caller,
        CancellationToken cancellationToken)
    {
        return _authorizer.RunAsManagerAsync(
            sessionId,
            caller,
            () => CreateArchiveCoreAsync(sessionId, cancellationToken),
            cancellationToken);
    }

    private async Task<ErrorOr<SlideshowArchive>> CreateArchiveCoreAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await _sessionsRepository.GetAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return SessionErrors.NotFound();
        }

        var path = await _imagesRepository.CreateSessionArchiveAsync(SessionDirectory(sessionId), cancellationToken);
        if (path is null)
        {
            return Error.NotFound(description: "The session has no photos");
        }

        // The code is short and made of safe characters, unlike the name
        return new SlideshowArchive(path, $"partyking-{session.Code}-photos.zip");
    }

    private string SessionDirectory(Guid sessionId) => $"{_configuration.UploadedPhotosDirectory}/{sessionId}";
}
