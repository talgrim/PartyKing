using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities;
using PartyKing.Domain.Enums;
using PartyKing.Domain.Models.Slideshow;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

public interface ISlideshowImagesRepository
{
    bool IsInitialized { get; }
    void UpdateSettings(bool autoRepeat, string placeholdersPath);
    Task AddSlideshowImageAsync(SlideshowImageWriteModel slideshowImage, CancellationToken cancellationToken);
    Task<SlideshowImageReadModel?> GetSlideshowImageAsync(CancellationToken cancellationToken);
}

internal class SlideshowImagesRepository : ISlideshowImagesRepository
{
    private readonly IDbContextFactory<ReaderDbContext> _readerFactory;
    private readonly IDbContextFactory<WriterDbContext> _writerFactory;

    private const string ImagesMask = "*.png|*.jpg|*.gif|*.bmp|*.jpeg";

    private Guid? _currentUploadedImageId;

    private bool _autoRepeat;
    private PlaceholdersCollection _placeholders = null!;

    public bool IsInitialized { get; private set; }

    public SlideshowImagesRepository(
        IDbContextFactory<ReaderDbContext> readerFactory,
        IDbContextFactory<WriterDbContext> writerFactory)
    {
        _readerFactory = readerFactory;
        _writerFactory = writerFactory;
    }

    public void UpdateSettings(bool autoRepeat, string placeholdersPath)
    {
        _autoRepeat = autoRepeat;
        var allFiles = Directory.GetFiles(placeholdersPath, ImagesMask, SearchOption.AllDirectories);

        _placeholders = new PlaceholdersCollection(allFiles.Select(x => x[placeholdersPath.Length..]).ToList());

        IsInitialized = true;
    }

    public async Task AddSlideshowImageAsync(SlideshowImageWriteModel slideshowImage, CancellationToken cancellationToken)
    {
        await AddImageDataToDbAsync(slideshowImage, cancellationToken);
        await SaveImageToFileAsync(slideshowImage, cancellationToken);
    }

    public async Task<SlideshowImageReadModel?> GetSlideshowImageAsync(CancellationToken cancellationToken)
    {
        var result = await GetUploadedImageAsync(cancellationToken);

        if (result is not null)
        {
            return result.ToReadModel(SlideshowImageSource.Uploaded);
        }

        return GetPlaceholderImage();
    }

    private async Task<SlideshowImage?> GetUploadedImageAsync(CancellationToken cancellationToken)
    {
        await using var context = await _readerFactory.CreateDbContextAsync(cancellationToken);

        SlideshowImage? result;
        if (!_currentUploadedImageId.HasValue)
        {
            result = await GetFirstImageAsync(context, cancellationToken);
        }
        else
        {
            result = await context.Images.FirstOrDefaultAsync(x => x.Id > _currentUploadedImageId.Value, cancellationToken);

            if (result is null)
            {
                result = await GetFirstImageAsync(context, cancellationToken);
            }
            else
            {
                _currentUploadedImageId = result.Id;
            }
        }

        return result;
    }

    private SlideshowImageReadModel GetPlaceholderImage()
    {
        var imageUrl = _placeholders.GetNext();

        return new SlideshowImageReadModel(imageUrl, GetContentType(), SlideshowImageSource.Placeholder);

        string GetContentType()
        {
            var extension = Path.GetExtension(imageUrl);
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

    private async Task<SlideshowImage?> GetFirstImageAsync(
        ReaderDbContext context,
        CancellationToken cancellationToken)
    {
        var result = await context.Images.OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        _currentUploadedImageId = result?.Id;
        return result;
    }

    private async Task AddImageDataToDbAsync(SlideshowImageWriteModel slideshowImage, CancellationToken cancellationToken)
    {
        await using var context = await _writerFactory.CreateDbContextAsync(cancellationToken);

        await context.Images.AddAsync(new SlideshowImage(slideshowImage), cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SaveImageToFileAsync(
        SlideshowImageWriteModel slideshowImage,
        CancellationToken cancellationToken)
    {
        var fileStream = File.Create(slideshowImage.GetFullPath());
        slideshowImage.Data.Seek(0, SeekOrigin.Begin);
        await slideshowImage.Data.CopyToAsync(fileStream, cancellationToken);
        fileStream.Close();
    }

    private record PlaceholdersCollection(IReadOnlyList<string> Placeholders)
    {
        private int _counter;

        public string GetNext()
        {
            var result = Placeholders[_counter++];
            if (_counter >= Placeholders.Count)
            {
                _counter = 0;
            }
            return result;
        }
    }
}