using Microsoft.AspNetCore.Http;
using PartyKing.Application.System;
using PartyKing.Domain.Models.Slideshow;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Application.Slideshow.Services;

public interface ISlideshowService
{
    bool IsInitialized();
    void UpdateSettings(bool autoRepeat, string placeholdersPath);
    Task UploadImagesAsync(IFormFile[] files, string root, CancellationToken cancellationToken);
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

    public bool IsInitialized()
    {
        return _imagesRepository.IsInitialized;
    }

    public void UpdateSettings(bool autoRepeat, string placeholdersPath)
    {
        _imagesRepository.UpdateSettings(autoRepeat, placeholdersPath);
    }

    public async Task UploadImagesAsync(IFormFile[] files, string root, CancellationToken cancellationToken)
    {
        await Parallel.ForEachAsync(files, cancellationToken, async (file, stoppingToken) => await UploadImageAsync(file, root, stoppingToken));
    }

    public Task<SlideshowImageReadModel?> GetImageAsync(CancellationToken cancellationToken)
    {
        return _imagesRepository.GetSlideshowImageAsync(cancellationToken);
    }

    private async Task UploadImageAsync(IFormFile file, string root, CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();

        await _imagesRepository.AddSlideshowImageAsync(CreateModel(), cancellationToken);

        return;

        SlideshowImageWriteModel CreateModel()
        {
            return new SlideshowImageWriteModel(file.FileName, file.ContentType, content, root);
        }
    }
}