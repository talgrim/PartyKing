using Microsoft.AspNetCore.Http;
using PartyKing.Application.Slideshow.Helpers;
using PartyKing.Application.System;
using PartyKing.Domain.Models.Slideshow;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Application.Slideshow.Services;

public interface ISlideshowService
{
    Task UploadImageAsync(IFormFile file, CancellationToken cancellationToken);
    Task<SlideshowImage?> GetImageAsync(CancellationToken cancellationToken);
}

public class SlideshowService : ISlideshowService
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISlideshowImagesRepository _imagesRepository;

    public SlideshowService(
        IDateTimeProvider dateTimeProvider,
        ISlideshowImagesRepository imagesRepository)
    {
        _dateTimeProvider = dateTimeProvider;
        _imagesRepository = imagesRepository;
    }

    public async Task UploadImageAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var imageData = await ImageDataHelper.PrepareImageDataAsync(file);

        await _imagesRepository.AddSlideshowImageAsync(CreateImage(), cancellationToken);

        return;

        SlideshowImage CreateImage()
        {
            return new SlideshowImage
            {
                Id = Guid.CreateVersion7(_dateTimeProvider.UtcNow),
                ImageData = imageData.ImageData,
                ImageUrl = file.FileName,
                ContentType = file.ContentType,
            };
        }
    }

    public Task<SlideshowImage?> GetImageAsync(CancellationToken cancellationToken)
    {
        return _imagesRepository.GetSlideshowImageAsync(cancellationToken);
    }
}