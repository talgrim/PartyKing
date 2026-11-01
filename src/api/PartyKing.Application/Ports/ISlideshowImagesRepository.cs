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
}
