using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PartyKing.Domain.Entities;
using PartyKing.Domain.Enums;
using PartyKing.Domain.Models.Slideshow;
using PartyKing.Infrastructure.Results;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

public interface ISlideshowImagesRepository
{
    bool IsInitialized { get; }
    TimeSpan SlideTime { get; }
    Task InitializeAsync(string rootPath, string placeholdersPath, CancellationToken cancellationToken);
    Task UpdateSettingsAsync(SlideshowSettings settings, CancellationToken cancellationToken);
    Task AddSlideshowImageAsync(SlideshowImageWriteModel slideshowImage, CancellationToken cancellationToken);
    Task<SlideshowImageReadResult> GetSlideshowImageAsync(Guid? currentId, CancellationToken cancellationToken);
    Task DeleteImageAsync(SlideshowImageReadResult image, CancellationToken cancellationToken);
}

internal class SlideshowImagesRepository : ISlideshowImagesRepository
{
    private static readonly string[] ImagesExtensions = [".png", ".jpg", ".gif", ".bmp", ".jpeg"];

    private readonly IDbContextFactory<ReaderDbContext> _readerFactory;
    private readonly IDbContextFactory<WriterDbContext> _writerFactory;
    private readonly ILogger<SlideshowImagesRepository> _logger;

    private SlideshowSettings _currentSettings = null!;

    private PlaceholdersCollection _placeholders = null!;
    private string _rootPath = null!;
    private string _placeholdersPath = null!;

    public bool IsInitialized { get; private set; }
    public TimeSpan SlideTime => _currentSettings.SlideTime;

    public SlideshowImagesRepository(
        IDbContextFactory<ReaderDbContext> readerFactory,
        IDbContextFactory<WriterDbContext> writerFactory,
        ILogger<SlideshowImagesRepository> logger)
    {
        _readerFactory = readerFactory;
        _writerFactory = writerFactory;
        _logger = logger;
    }

    public async Task InitializeAsync(string rootPath, string placeholdersPath, CancellationToken cancellationToken)
    {
        _rootPath = rootPath;
        _placeholdersPath = placeholdersPath;

        var allFiles = Directory.GetFiles(
            Path.Combine(_rootPath, _placeholdersPath),
            "*.*",
            SearchOption.AllDirectories);

        var imageFiles = allFiles
            .Where(x => ImagesExtensions.Contains(Path.GetExtension(x), StringComparer.OrdinalIgnoreCase))
            .ToList();

        _placeholders = new PlaceholdersCollection(imageFiles.Select(x => x[rootPath.Length..]).ToList());

        await LoadSettingsAsync(cancellationToken);

        IsInitialized = true;

        _logger.LogInformation(
            "Initialized settings. Root path: {Root}, Placeholders path: {Placeholders}, Placeholders loaded: {Count}",
            rootPath,
            _placeholdersPath,
            _placeholders.Placeholders.Count);
    }

    public async Task UpdateSettingsAsync(SlideshowSettings settings, CancellationToken cancellationToken)
    {
        _currentSettings = settings;

        await UpdateSettingsInDatabaseAsync(cancellationToken);
    }

    public async Task AddSlideshowImageAsync(SlideshowImageWriteModel slideshowImage, CancellationToken cancellationToken)
    {
        await AddImageDataToDbAsync(slideshowImage, cancellationToken);
        await SaveImageToFileAsync(slideshowImage, cancellationToken);
    }

    public async Task<SlideshowImageReadResult> GetSlideshowImageAsync(Guid? currentId, CancellationToken cancellationToken)
    {
        if(currentId is not null)
        {
            await MarkImageAsPresentedAsync(currentId.Value, cancellationToken);
        }

        var result = await GetUploadedImageAsync(currentId, cancellationToken);

        var data = result?.ToReadModel(SlideshowImageSource.Uploaded) ?? GetPlaceholderImage();

        return new SlideshowImageReadResult(result?.Id ?? currentId, data);
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

    private async Task<SlideshowImage?> GetUploadedImageAsync(Guid? currentId, CancellationToken cancellationToken)
    {
        await using var context = await _readerFactory.CreateDbContextAsync(cancellationToken);

        var result = await context.Images
            .Where(x => _currentSettings.StartFromBeginning || !x.WasPresented)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(x => x.Id > (currentId ?? Guid.Empty), cancellationToken);

        if (result is not null)
        {
            return result;
        }

        if (currentId.HasValue && _currentSettings.AutoRepeat)
        {
            _logger.LogInformation("Reached the end of uploaded images. Repeating from begin");
            return await GetUploadedImageAsync(null, cancellationToken);
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

    private SlideshowImageReadModel GetPlaceholderImage()
    {
        var (fileName, url) = _placeholders.GetNext();

        return SlideshowImageReadModel.Placeholder(fileName, url, GetContentType());

        string GetContentType()
        {
            var extension = Path.GetExtension(fileName);
            return extension switch
            {
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                _ => "image/jpeg",
            };
        }
    }

    private async Task AddImageDataToDbAsync(SlideshowImageWriteModel slideshowImage, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Adding image data to database. FileName: {FileName}, Content Type: {ContentType}",
            slideshowImage.ImageName,
            slideshowImage.ContentType);

        try
        {
            await using var context = await _writerFactory.CreateDbContextAsync(cancellationToken);

            await context.Images.AddAsync(new SlideshowImage(slideshowImage), cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add image data to database. FileName: {FileName}", slideshowImage.ImageName);
        }
    }

    private async Task SaveImageToFileAsync(
        SlideshowImageWriteModel slideshowImage,
        CancellationToken cancellationToken)
    {
        var filePath = slideshowImage.GetFullPath();
        _logger.LogInformation("Saving image to file: {FilePath}", filePath);

        FileStream? fileStream = null;
        try
        {
            fileStream = File.Create(Path.Combine(_rootPath, filePath));
            slideshowImage.Data.Seek(0, SeekOrigin.Begin);
            await slideshowImage.Data.CopyToAsync(fileStream, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to save image to file: {FilePath}", filePath);
        }
        finally
        {
            fileStream?.Close();
        }
    }

    private async Task LoadSettingsAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _readerFactory.CreateDbContextAsync(cancellationToken);
        _currentSettings = await dbContext.SlideshowSettings.AsNoTracking().FirstAsync(cancellationToken);
    }

    private async Task UpdateSettingsInDatabaseAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);
        dbContext.SlideshowSettings.Update(_currentSettings);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkImageAsPresentedAsync(Guid currentId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerFactory.CreateDbContextAsync(cancellationToken);
        var currentImage = await dbContext.Images.FirstAsync(x => x.Id == currentId, cancellationToken);
        currentImage.WasPresented = true;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private record PlaceholdersCollection(IReadOnlyList<string> Placeholders)
    {
        private int _counter;

        public (string? FileName, string? Url) GetNext()
        {
            var result = Placeholders.ElementAtOrDefault(_counter++);
            if (_counter >= Placeholders.Count)
            {
                _counter = 0;
            }

            var fileName = Path.GetFileName(result);
            return (fileName, result);
        }
    }
}