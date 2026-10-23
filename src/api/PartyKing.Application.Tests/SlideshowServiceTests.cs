using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Slideshow.Services;
using PartyKing.Domain.Entities;
using PartyKing.Domain.Models.Slideshow;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Infrastructure.Results;

namespace PartyKing.Application.Tests;

public class SlideshowServiceTests
{
    private static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1];

    private readonly FakeClock _clock = new();
    private readonly FakeSlideshowImagesRepository _repository = new();
    private readonly SlideshowService _service;

    public SlideshowServiceTests()
    {
        var configuration = new SlideshowConfiguration
        {
            UploadedPhotosDirectory = "SampleImages/Uploaded",
            PlaceholderPhotosDirectory = "SampleImages/Placeholder",
            SlideTime = TimeSpan.FromSeconds(5)
        };
        _service = new SlideshowService(_clock, _repository, Options.Create(configuration));
    }

    private static IFormFile File(string name, byte[] content) => new FakeFormFile(name, content);

    [Fact]
    public async Task GetImage_WithNothingToShow_ReturnsNull()
    {
        _repository.NextImage = new SlideshowImageReadResult(null, null);

        var result = await _service.GetImageAsync(CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetImage_AfterImagesAppear_ReturnsThemOnTheNextSlide()
    {
        _repository.NextImage = new SlideshowImageReadResult(null, null);
        await _service.GetImageAsync(CancellationToken.None);

        _clock.Advance(TimeSpan.FromSeconds(6));
        _repository.NextImage = new SlideshowImageReadResult(
            Guid.NewGuid(),
            new SlideshowImageReadModel("a.jpg", "SampleImages/Uploaded/a.jpg", "image/jpeg", Domain.Enums.SlideshowImageSource.Uploaded, false));

        var result = await _service.GetImageAsync(CancellationToken.None);

        Assert.Equal("SampleImages/Uploaded/a.jpg", result!.FileName);
    }

    [Fact]
    public async Task Upload_StoresTheDetectedFormat_NotTheClientsExtensionOrContentType()
    {
        var file = File("holiday.JPEG", JpegBytes);

        await _service.UploadImagesAsync([file], deleteAfterPresentation: false, CancellationToken.None);

        var stored = Assert.Single(_repository.Added);
        Assert.Equal(".jpg", stored.Extension);
        Assert.Equal("image/jpeg", stored.ContentType);
    }

    [Fact]
    public async Task Upload_WithOneInvalidFile_StoresNothingFromTheBatch()
    {
        var good = File("a.jpg", JpegBytes);
        var bad = File("evil.html", "<script>alert(1)</script>"u8.ToArray());

        await Assert.ThrowsAsync<InvalidUploadException>(
            () => _service.UploadImagesAsync([good, bad], deleteAfterPresentation: false, CancellationToken.None));

        Assert.Empty(_repository.Added);
    }

    private class FakeFormFile(string fileName, byte[] content) : IFormFile
    {
        public string ContentType => "text/html"; // whatever the client claims must not matter
        public string ContentDisposition => string.Empty;
        public IHeaderDictionary Headers => throw new NotSupportedException();
        public long Length => content.Length;
        public string Name => "files";
        public string FileName => fileName;

        public Stream OpenReadStream() => new MemoryStream(content);

        public void CopyTo(Stream target) => target.Write(content);

        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) =>
            target.WriteAsync(content, cancellationToken).AsTask();
    }

    private class FakeSlideshowImagesRepository : ISlideshowImagesRepository
    {
        public SlideshowImageReadResult NextImage { get; set; } = new(null, null);
        public List<SlideshowImageWriteModel> Added { get; } = [];

        public TimeSpan SlideTime => TimeSpan.FromSeconds(5);

        public Task InitializeAsync(string rootPath, string placeholdersPath, string uploadedPath, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task UpdateSettingsAsync(SlideshowSettings settings, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AddSlideshowImageAsync(SlideshowImageWriteModel slideshowImage, CancellationToken cancellationToken)
        {
            Added.Add(slideshowImage);
            return Task.CompletedTask;
        }

        public Task<SlideshowImageReadResult> GetSlideshowImageAsync(Guid? currentId, CancellationToken cancellationToken) =>
            Task.FromResult(NextImage);

        public Task DeleteImageAsync(SlideshowImageReadResult image, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
