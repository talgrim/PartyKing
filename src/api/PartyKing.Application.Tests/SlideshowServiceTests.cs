using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Slideshow.Services;
using PartyKing.Contract.V1.Slideshow;
using PartyKing.Domain.Entities.Slideshow;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Infrastructure.Results;

namespace PartyKing.Application.Tests;

public class SlideshowServiceTests
{
    private static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1];

    private readonly FakeClock _clock = new();
    private readonly FakeSlideshowImagesRepository _repository = new();
    private readonly FakeSessionsRepository _sessions = new();
    private readonly SlideshowService _service;
    private readonly Guid _sessionId;

    public SlideshowServiceTests()
    {
        var configuration = new SlideshowConfiguration
        {
            UploadedPhotosDirectory = "Images/Uploaded",
            SlideTime = TimeSpan.FromSeconds(5)
        };
        _service = new SlideshowService(_clock, _repository, _sessions, Options.Create(configuration));
        _sessionId = _sessions.AddSession().Id;
    }

    private static IFormFile File(string name, byte[] content) => new FakeFormFile(name, content);

    private static SlideshowImageReadResult Image(string name) =>
        new(Guid.NewGuid(), new SlideshowImageReadModel(name, $"Images/Uploaded/{name}", "image/jpeg", false));

    [Fact]
    public async Task GetImage_WithNothingToShow_ReturnsNull()
    {
        var result = await _service.GetImageAsync(_sessionId, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetImage_AfterImagesAppear_ReturnsThemOnTheNextSlide()
    {
        await _service.GetImageAsync(_sessionId, CancellationToken.None);

        _clock.Advance(TimeSpan.FromSeconds(6));
        _repository.NextImages[_sessionId] = Image("a.jpg");

        var result = await _service.GetImageAsync(_sessionId, CancellationToken.None);

        Assert.Equal("Images/Uploaded/a.jpg", result!.FileName);
    }

    [Fact]
    public async Task GetImage_KeepsTheCurrentImageUntilItExpires()
    {
        _repository.NextImages[_sessionId] = Image("a.jpg");
        await _service.GetImageAsync(_sessionId, CancellationToken.None);

        _repository.NextImages[_sessionId] = Image("b.jpg");
        var result = await _service.GetImageAsync(_sessionId, CancellationToken.None);

        Assert.Equal("Images/Uploaded/a.jpg", result!.FileName);
    }

    [Fact]
    public async Task GetImage_KeepsEverySessionsSlideSeparate()
    {
        var other = _sessions.AddSession().Id;
        _repository.NextImages[_sessionId] = Image("a.jpg");
        _repository.NextImages[other] = Image("b.jpg");

        var first = await _service.GetImageAsync(_sessionId, CancellationToken.None);
        var second = await _service.GetImageAsync(other, CancellationToken.None);

        Assert.Equal("Images/Uploaded/a.jpg", first!.FileName);
        Assert.Equal("Images/Uploaded/b.jpg", second!.FileName);
    }

    [Fact]
    public async Task GetImage_UsesTheSessionsSlideTime()
    {
        _repository.Settings[_sessionId] = new SlideshowSettings { SessionId = _sessionId, SlideTime = TimeSpan.FromSeconds(30) };
        _repository.NextImages[_sessionId] = Image("a.jpg");

        var result = await _service.GetImageAsync(_sessionId, CancellationToken.None);

        Assert.Equal(_clock.UtcNow.AddSeconds(30), result!.ExpirationDate);
    }

    [Fact]
    public async Task GetImage_DeletesAPresentedImageMarkedForDeletion()
    {
        var image = new SlideshowImageReadResult(
            Guid.NewGuid(),
            new SlideshowImageReadModel("a.jpg", "Images/Uploaded/a.jpg", "image/jpeg", true));
        _repository.NextImages[_sessionId] = image;
        await _service.GetImageAsync(_sessionId, CancellationToken.None);

        _clock.Advance(TimeSpan.FromSeconds(6));
        await _service.GetImageAsync(_sessionId, CancellationToken.None);

        Assert.Equal([image], _repository.Deleted);
    }

    [Fact]
    public async Task GetSettings_OfASessionWithoutOwnSettings_ReturnsTheDefaults()
    {
        var result = await _service.GetSettingsAsync(_sessionId, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(TimeSpan.FromSeconds(5), result.Value.SlideTime);
        Assert.True(result.Value.AutoRepeat);
        Assert.True(result.Value.StartFromBeginning);
    }

    [Fact]
    public async Task GetSettings_OfAnUnknownSession_IsNotFound()
    {
        var result = await _service.GetSettingsAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task UpdateSettings_SavesThemForThatSession()
    {
        var settings = new SlideshowSettingsDto { AutoRepeat = true, SlideTime = TimeSpan.FromSeconds(12) };

        var result = await _service.UpdateSettingsAsync(_sessionId, settings, CancellationToken.None);

        Assert.False(result.IsError);
        var saved = _repository.Settings[_sessionId];
        Assert.True(saved.AutoRepeat);
        Assert.Equal(TimeSpan.FromSeconds(12), saved.SlideTime);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(7200)]
    public async Task UpdateSettings_WithAnUnreasonableSlideTime_IsRejected(int seconds)
    {
        var settings = new SlideshowSettingsDto { SlideTime = TimeSpan.FromSeconds(seconds) };

        var result = await _service.UpdateSettingsAsync(_sessionId, settings, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Empty(_repository.Settings);
    }

    [Fact]
    public async Task UpdateSettings_OfAnUnknownSession_IsNotFound()
    {
        var result = await _service.UpdateSettingsAsync(Guid.NewGuid(), new SlideshowSettingsDto(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Empty(_repository.Settings);
    }

    [Fact]
    public async Task Upload_StoresTheDetectedFormat_NotTheClientsExtensionOrContentType()
    {
        var file = File("holiday.JPEG", JpegBytes);

        await _service.UploadImagesAsync(_sessionId, [file], deleteAfterPresentation: false, CancellationToken.None);

        var stored = Assert.Single(_repository.Added);
        Assert.Equal(".jpg", stored.Extension);
        Assert.Equal("image/jpeg", stored.ContentType);
    }

    [Fact]
    public async Task Upload_StoresTheImageInTheSessionsDirectory()
    {
        await _service.UploadImagesAsync(_sessionId, [File("a.jpg", JpegBytes)], deleteAfterPresentation: true, CancellationToken.None);

        var stored = Assert.Single(_repository.Added);
        Assert.Equal(_sessionId, stored.SessionId);
        Assert.Equal($"Images/Uploaded/{_sessionId}", stored.Path);
        Assert.True(stored.DeleteAfterPresentation);
    }

    [Fact]
    public async Task Upload_WithOneInvalidFile_StoresNothingFromTheBatch()
    {
        var good = File("a.jpg", JpegBytes);
        var bad = File("evil.html", "<script>alert(1)</script>"u8.ToArray());

        await Assert.ThrowsAsync<InvalidUploadException>(
            () => _service.UploadImagesAsync(_sessionId, [good, bad], deleteAfterPresentation: false, CancellationToken.None));

        Assert.Empty(_repository.Added);
    }

    [Fact]
    public async Task DeleteSession_DeletesItsFilesAndForgetsItsSlide()
    {
        _repository.NextImages[_sessionId] = Image("a.jpg");
        await _service.GetImageAsync(_sessionId, CancellationToken.None);

        await _service.DeleteSessionAsync(_sessionId);

        Assert.Equal([$"Images/Uploaded/{_sessionId}"], _repository.DeletedDirectories);

        // The slide was forgotten, so the next poll looks the image up again instead of serving the old one
        _repository.NextImages.Remove(_sessionId);
        Assert.Null(await _service.GetImageAsync(_sessionId, CancellationToken.None));
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
        public Dictionary<Guid, SlideshowImageReadResult> NextImages { get; } = [];
        public Dictionary<Guid, SlideshowSettings> Settings { get; } = [];
        public List<SlideshowImageWriteModel> Added { get; } = [];
        public List<SlideshowImageReadResult> Deleted { get; } = [];
        public List<string> DeletedDirectories { get; } = [];

        public Task InitializeAsync(string rootPath, string uploadedPath, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<SlideshowSettings?> GetSettingsAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(Settings.GetValueOrDefault(sessionId));

        public Task SaveSettingsAsync(SlideshowSettings settings, CancellationToken cancellationToken)
        {
            Settings[settings.SessionId] = settings;
            return Task.CompletedTask;
        }

        public Task AddSlideshowImageAsync(SlideshowImageWriteModel slideshowImage, CancellationToken cancellationToken)
        {
            Added.Add(slideshowImage);
            return Task.CompletedTask;
        }

        public Task<SlideshowImageReadResult> GetSlideshowImageAsync(
            Guid sessionId,
            Guid? currentId,
            SlideshowSettings settings,
            CancellationToken cancellationToken) =>
            Task.FromResult(NextImages.GetValueOrDefault(sessionId) ?? new SlideshowImageReadResult(currentId, null));

        public Task DeleteImageAsync(SlideshowImageReadResult image, CancellationToken cancellationToken)
        {
            Deleted.Add(image);
            return Task.CompletedTask;
        }

        public Task DeleteSessionFilesAsync(string sessionDirectory)
        {
            DeletedDirectories.Add(sessionDirectory);
            return Task.CompletedTask;
        }
    }
}
