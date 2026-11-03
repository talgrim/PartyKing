using PartyKing.Domain.Entities.Slideshow;

namespace PartyKing.Application.Ports;

/// <summary>The slideshow's image files and the database rows that describe them.</summary>
public interface ISlideshowImagesRepository
{
    Task InitializeAsync(string rootPath, string uploadedPath, CancellationToken cancellationToken);
    Task AddSlideshowImageAsync(SlideshowImageWriteModel slideshowImage, CancellationToken cancellationToken);
    Task<SlideshowImageReadResult> GetSlideshowImageAsync(
        Guid sessionId,
        Guid? currentId,
        SlideshowSettings settings,
        CancellationToken cancellationToken);
    Task DeleteImageAsync(SlideshowImageReadResult image, CancellationToken cancellationToken);

    /// <summary>Removes the session's files; its database rows go with the session itself.</summary>
    Task DeleteSessionFilesAsync(string sessionDirectory);

    /// <summary>How many photo files the session's folder holds.</summary>
    int CountSessionPhotos(string sessionDirectory);

    /// <summary>
    /// Writes the session's photos, in upload order and named 001.ext, 002.ext, ..., into a zip in a temp folder and
    /// returns its path (the caller deletes it), or null when the session has no photos.
    /// </summary>
    Task<string?> CreateSessionArchiveAsync(string sessionDirectory, CancellationToken cancellationToken);
}
