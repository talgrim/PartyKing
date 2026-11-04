using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PartyKing.Application.Events;
using PartyKing.Domain.Entities.Slideshow;
using PartyKing.Application.Ports;
using PartyKing.Domain.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

internal class SlideshowImagesRepository : ISlideshowImagesRepository, ISlideshowSettingsRepository
{
    // Uploads used to live under this folder, shared by all sessions
    private const string LegacyImagesDirectory = "SampleImages";

    // The second attempt finds the row the racing save inserted; settings rows are never deleted, bar the cascade of a session
    private const int MaxSettingsAttempts = 2;

    // Archives are built here and deleted once the download ends; leftovers of a crashed process are swept at startup
    private static readonly string ArchiveDirectory = Path.Combine(Path.GetTempPath(), "partyking-archives");

    private readonly IDbContextFactory<ReaderDbContext> _readerFactory;
    private readonly IDbContextFactory<WriterDbContext> _writerFactory;
    private readonly ILogger<SlideshowImagesRepository> _logger;
    private readonly IDomainEventDispatcher _dispatcher;

    private string _rootPath = null!;

    public SlideshowImagesRepository(
        IDbContextFactory<ReaderDbContext> readerFactory,
        IDbContextFactory<WriterDbContext> writerFactory,
        ILogger<SlideshowImagesRepository> logger,
        IDomainEventDispatcher dispatcher)
    {
        _readerFactory = readerFactory;
        _writerFactory = writerFactory;
        _logger = logger;
        _dispatcher = dispatcher;
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

        if (Directory.Exists(ArchiveDirectory))
        {
            DeleteDirectoryQuietly(ArchiveDirectory);
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

    public async Task<SlideshowSettingsFailure> ChangeAsync(
        Guid sessionId,
        bool autoRepeat,
        bool startFromBeginning,
        TimeSpan? slideTime,
        TimeSpan defaultSlideTime,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);

            var settings = await dbContext.SlideshowSettings
                .FirstOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);
            var isNew = settings is null;
            settings ??= SlideshowSettings.Defaults(sessionId, defaultSlideTime);

            var failure = settings.Change(autoRepeat, startFromBeginning, slideTime, defaultSlideTime);
            if (failure != SlideshowSettingsFailure.None)
            {
                return failure;
            }

            if (isNew)
            {
                dbContext.SlideshowSettings.Add(settings);
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (isNew && ex.IsUniqueViolation() && attempt < MaxSettingsAttempts)
            {
                // Another save inserted the session's row between the read and ours: start again on top of it
                continue;
            }

            // Not tied to the request: an aborted request must not drop the follow-up of a change that is saved
            await _dispatcher.DispatchAsync(settings.DrainEvents(), CancellationToken.None);

            return SlideshowSettingsFailure.None;
        }
    }

    public async Task AddSlideshowImageAsync(SlideshowImageWriteModel slideshowImage, CancellationToken cancellationToken)
    {
        var image = new SlideshowImage(
            slideshowImage.SessionId,
            slideshowImage.Extension,
            slideshowImage.ContentType,
            slideshowImage.Path,
            slideshowImage.DeleteAfterPresentation);

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
        // One context for both halves: the image being left is marked and the next one chosen in a single save
        await using var context = await _writerFactory.CreateDbContextAsync(cancellationToken);

        var images = await context.Images.Where(x => x.SessionId == sessionId).ToListAsync(cancellationToken);
        var step = SlideshowPlaylist.Of(sessionId, images).Advance(currentId, settings);

        await context.SaveChangesAsync(cancellationToken);

        if (step.Repeated)
        {
            _logger.LogInformation("Reached the end of uploaded images. Repeating from begin");
        }
        else if (step.Image is null)
        {
            _logger.LogDebug("Auto repeat is off and last uploaded image was already viewed");
        }

        return new SlideshowImageReadResult(step.Image?.Id ?? currentId, step.Image?.ToReadModel());
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

    public int CountSessionPhotos(string sessionDirectory)
    {
        var directory = Path.Combine(_rootPath, sessionDirectory);

        return Directory.Exists(directory) ? Directory.EnumerateFiles(directory).Count() : 0;
    }

    public async Task<string?> CreateSessionArchiveAsync(string sessionDirectory, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(_rootPath, sessionDirectory);
        if (!Directory.Exists(directory))
        {
            return null;
        }

        // File names are GUIDv7, so the name order is the upload order
        var files = Directory.EnumerateFiles(directory)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();
        if (files.Count == 0)
        {
            return null;
        }

        Directory.CreateDirectory(ArchiveDirectory);
        var archivePath = Path.Combine(ArchiveDirectory, $"{Guid.NewGuid():N}.zip");
        var digits = Math.Max(3, files.Count.ToString().Length);
        var written = 0;

        try
        {
            await using var target = new FileStream(
                archivePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);
            using var archive = new ZipArchive(target, ZipArchiveMode.Create);

            for (var i = 0; i < files.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                FileStream source;
                try
                {
                    source = new FileStream(
                        files[i],
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete,
                        bufferSize: 81920,
                        useAsync: true);
                }
                catch (FileNotFoundException)
                {
                    // A DeleteAfterPresentation photo was rotated out after the listing
                    continue;
                }

                await using (source)
                {
                    var entryName = $"{(++written).ToString().PadLeft(digits, '0')}{Path.GetExtension(files[i])}";
                    // Photos are already compressed, so deflating them again only costs CPU
                    var entry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);
                    await using var entryStream = entry.Open();
                    await source.CopyToAsync(entryStream, cancellationToken);
                }
            }
        }
        catch
        {
            TryDeleteFile(archivePath);
            throw;
        }

        return archivePath;
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to delete archive {Path}", path);
        }
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
}
