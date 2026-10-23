using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PartyKing.Domain.Entities;
using PartyKing.Domain.Models.Slideshow;
using PartyKing.Infrastructure.Results;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

public interface ISlideshowImagesRepository
{
    Task InitializeAsync(string rootPath, string uploadedPath, CancellationToken cancellationToken);

    /// <summary>The session's settings, or null when it never changed them.</summary>
    Task<SlideshowSettings?> GetSettingsAsync(Guid sessionId, CancellationToken cancellationToken);
    Task SaveSettingsAsync(SlideshowSettings settings, CancellationToken cancellationToken);
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

internal class SlideshowImagesRepository : ISlideshowImagesRepository
{
    // Uploads used to live under this folder, shared by all sessions
    private const string LegacyImagesDirectory = "SampleImages";

    private readonly IDbContextFactory<ReaderDbContext> _readerFactory;
    private readonly IDbContextFactory<WriterDbContext> _writerFactory;
    private readonly ILogger<SlideshowImagesRepository> _logger;

    private string _rootPath = null!;

    public SlideshowImagesRepository(
        IDbContextFactory<ReaderDbContext> readerFactory,
        IDbContextFactory<WriterDbContext> writerFactory,
        ILogger<SlideshowImagesRepository> logger)
    {
        _readerFactory = readerFactory;
        _writerFactory = writerFactory;
        _logger = logger;
    }

    public Task InitializeAsync(string rootPath, string uploadedPath, CancellationToken cancellationToken)
    {
        _rootPath = rootPath;

        // The folder doesn't ship with the app: a fresh deployment starts without any images
        Directory.CreateDirectory(Path.Combine(_rootPath, uploadedPath));

        // The migration to per-session slideshows deleted the rows of the old shared uploads, so their files are orphans
        var legacyDirectory = Path.Combine(_rootPath, LegacyImagesDirectory);
        if (Directory.Exists(legacyDirectory))
        {
            DeleteDirectoryQuietly(legacyDirectory);
        }

        _logger.LogInformation("Initialized slideshow. Root path: {Root}, Uploads path: {Uploads}", rootPath, uploadedPath);

        return Task.CompletedTask;
    }

    public async Task<SlideshowSettings?> GetSettingsAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _readerFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.SlideshowSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);
    }

    public async Task SaveSettingsAsync(SlideshowSettings settings, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

        var updated = await dbContext.SlideshowSettings
            .Where(x => x.SessionId == settings.SessionId)
            .ExecuteUpdateAsync(
                x => x
                    .SetProperty(s => s.AutoRepeat, settings.AutoRepeat)
                    .SetProperty(s => s.StartFromBeginning, settings.StartFromBeginning)
                    .SetProperty(s => s.SlideTime, settings.SlideTime),
                cancellationToken);

        if (updated == 0)
        {
            dbContext.SlideshowSettings.Add(settings);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task AddSlideshowImageAsync(SlideshowImageWriteModel slideshowImage, CancellationToken cancellationToken)
    {
        var image = new SlideshowImage(slideshowImage);

        Directory.CreateDirectory(Path.Combine(_rootPath, slideshowImage.Path));

        // File first: a failed write must not leave a database row without a file
        await SaveImageToFileAsync(image, slideshowImage, cancellationToken);

        try
        {
            await AddImageDataToDbAsync(image, cancellationToken);
        }
        catch
        {
            DeleteFileQuietly(image);
            throw;
        }
    }

    public async Task<SlideshowImageReadResult> GetSlideshowImageAsync(
        Guid sessionId,
        Guid? currentId,
        SlideshowSettings settings,
        CancellationToken cancellationToken)
    {
        if (currentId is not null)
        {
            await MarkImageAsPresentedAsync(currentId.Value, cancellationToken);
        }

        var result = await GetUploadedImageAsync(sessionId, currentId, settings, cancellationToken);

        return new SlideshowImageReadResult(result?.Id ?? currentId, result?.ToReadModel());
    }

    public async Task DeleteImageAsync(SlideshowImageReadResult image, CancellationToken cancellationToken)
    {
        DeletePhotoFile(image.Data!);

        await using var context = await _writerFactory.CreateDbContextAsync(cancellationToken);

        var dbImage = await context.Images.FirstOrDefaultAsync(x => x.Id == image.ImageId, cancellationToken);
        if (dbImage is not null)
        {
            context.Remove(dbImage);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public Task DeleteSessionFilesAsync(string sessionDirectory)
    {
        var directory = Path.Combine(_rootPath, sessionDirectory);
        if (Directory.Exists(directory))
        {
            DeleteDirectoryQuietly(directory);
        }

        return Task.CompletedTask;
    }

    private async Task<SlideshowImage?> GetUploadedImageAsync(
        Guid sessionId,
        Guid? currentId,
        SlideshowSettings settings,
        CancellationToken cancellationToken)
    {
        await using var context = await _readerFactory.CreateDbContextAsync(cancellationToken);

        var result = await context.Images
            .Where(x => x.SessionId == sessionId && (settings.StartFromBeginning || !x.WasPresented))
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(x => x.Id > (currentId ?? Guid.Empty), cancellationToken);

        if (result is not null)
        {
            return result;
        }

        if (currentId.HasValue && settings.AutoRepeat)
        {
            _logger.LogInformation("Reached the end of uploaded images. Repeating from begin");
            return await GetUploadedImageAsync(sessionId, null, settings, cancellationToken);
        }

        _logger.LogDebug("Auto repeat is off and last uploaded image was already viewed");

        return result;
    }

    private void DeletePhotoFile(SlideshowImageReadModel currentPhoto)
    {
        _logger.LogInformation("Deleting photo {FileName} after presentation", currentPhoto.ImageName);
        try
        {
            File.Delete(Path.Combine(_rootPath, currentPhoto.ImageUrl));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to delete photo {FileName} after presentation", currentPhoto.ImageName);
        }
    }

    private async Task AddImageDataToDbAsync(SlideshowImage image, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Adding image data to database. FileName: {FileName}, Content Type: {ContentType}",
            image.ImageName,
            image.ContentType);

        await using var context = await _writerFactory.CreateDbContextAsync(cancellationToken);

        await context.Images.AddAsync(image, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task SaveImageToFileAsync(
        SlideshowImage image,
        SlideshowImageWriteModel slideshowImage,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Saving image to file: {FilePath}", image.ImageUrl);

        try
        {
            await using var fileStream = File.Create(Path.Combine(_rootPath, image.ImageUrl));
            slideshowImage.Data.Seek(0, SeekOrigin.Begin);
            await slideshowImage.Data.CopyToAsync(fileStream, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to save image to file: {FilePath}", image.ImageUrl);
            DeleteFileQuietly(image);
            throw;
        }
    }

    private void DeleteFileQuietly(SlideshowImage image)
    {
        try
        {
            File.Delete(Path.Combine(_rootPath, image.ImageUrl));
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to clean up {FilePath}", image.ImageUrl);
        }
    }

    private void DeleteDirectoryQuietly(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to delete directory {Directory}", directory);
        }
    }

    private async Task MarkImageAsPresentedAsync(Guid currentId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

        var currentImage = await dbContext.Images.FirstOrDefaultAsync(x => x.Id == currentId, cancellationToken);
        if (currentImage is null)
        {
            return;
        }

        currentImage.WasPresented = true;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
