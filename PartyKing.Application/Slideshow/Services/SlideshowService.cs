using Microsoft.AspNetCore.Http;
using PartyKing.Application.Slideshow.Helpers;
using PartyKing.Application.System;
using PartyKing.Domain.Models.Slideshow;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Application.Slideshow.Services;

public interface ISlideshowService
{
    Task UploadImageAsync(IFormFile file, CancellationToken cancellationToken);
    Task<SlideshowImageReadModel?> GetImageAsync(CancellationToken cancellationToken);
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
        await using var content = file.OpenReadStream();

        await _imagesRepository.AddSlideshowImageAsync(CreateModel(), cancellationToken);

        return;

        SlideshowImageWriteModel CreateModel()
        {
            return new SlideshowImageWriteModel
            {
                ImageUrl = file.FileName,
                ContentType = file.ContentType,
                Data = content
            };
        }
    }

    public Task<SlideshowImageReadModel?> GetImageAsync(CancellationToken cancellationToken)
    {
        return _imagesRepository.GetSlideshowImageAsync(cancellationToken);
    }
}