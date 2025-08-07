using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PartyKing.Domain.Entities;
using PartyKing.Domain.Models.Slideshow;
using PartyKing.Infrastructure.Configuration;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

public interface ISlideshowImagesRepository
{
    Task AddSlideshowImageAsync(SlideshowImageWriteModel slideshowImage, CancellationToken cancellationToken);
    Task<SlideshowImageReadModel?> GetSlideshowImageAsync(CancellationToken cancellationToken);
}

internal class SlideshowImagesRepository : ISlideshowImagesRepository
{
    private readonly IDbContextFactory<ReaderDbContext> _dbContextFactory;

    private Guid? _currentImageId;
    private readonly SlideshowSettings _slideshowSettings;

    public SlideshowImagesRepository(
        IOptions<SlideshowSettings> slideshowSettingsOptions,
        IDbContextFactory<ReaderDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
        _slideshowSettings = slideshowSettingsOptions.Value;
    }

    public async Task AddSlideshowImageAsync(SlideshowImageWriteModel slideshowImage, CancellationToken cancellationToken)
    {
        await AddImageDataToDbAsync(slideshowImage, cancellationToken);
        await SaveImageToFileAsync(slideshowImage, cancellationToken);
    }

    public async Task<SlideshowImageReadModel?> GetSlideshowImageAsync(CancellationToken cancellationToken)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        SlideshowImageWriteModel? result;

        if (!_currentImageId.HasValue)
        {
            result = await GetFirstImageAsync(context, cancellationToken);
        }
        else
        {
            result = await context.Images.FirstOrDefaultAsync(x => x.Id > _currentImageId.Value, cancellationToken);

            if (result is null)
            {
                result = await GetFirstImageAsync(context, cancellationToken);
            }
            else
            {
                _currentImageId = result.Id;
            }
        }

        return result;
    }

    private async Task<SlideshowImageWriteModel?> GetFirstImageAsync(
        ReaderDbContext context,
        CancellationToken cancellationToken)
    {
        var result = await context.Images.OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        _currentImageId = result?.Id;
        return result;
    }

    private async Task AddImageDataToDbAsync(SlideshowImageWriteModel slideshowImage, CancellationToken cancellationToken)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        await context.Images.AddAsync(new SlideshowImage(slideshowImage), cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task SaveImageToFileAsync(SlideshowImageWriteModel slideshowImage, CancellationToken cancellationToken)
    {
        var fileStream = File.Create(Path.Combine(_slideshowSettings.UploadedPhotosDirectory, slideshowImage.ImageUrl));
        slideshowImage.Data.Seek(0, SeekOrigin.Begin);
        await slideshowImage.Data.CopyToAsync(fileStream, cancellationToken);
        fileStream.Close();
    }
}