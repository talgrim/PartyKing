using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Models.Slideshow;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

public interface ISlideshowImagesRepository
{
    Task AddSlideshowImageAsync(SlideshowImage slideshowImage, CancellationToken cancellationToken);
    Task<SlideshowImage?> GetSlideshowImageAsync(CancellationToken cancellationToken);
}

internal class SlideshowImagesRepository : ISlideshowImagesRepository
{
    private readonly IDbContextFactory<ReaderDbContext> _dbContextFactory;

    private Guid? _currentImageId;

    public SlideshowImagesRepository(IDbContextFactory<ReaderDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task AddSlideshowImageAsync(SlideshowImage slideshowImage, CancellationToken cancellationToken)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        await context.Images.AddAsync(slideshowImage, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<SlideshowImage?> GetSlideshowImageAsync(CancellationToken cancellationToken)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        SlideshowImage? result;

        if (!_currentImageId.HasValue)
        {
            result = await context.Images.OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            result = await context.Images.FirstOrDefaultAsync(x => x.Id > _currentImageId.Value, cancellationToken);
        }

        _currentImageId = result?.Id;
        return result;
    }
}