using ErrorOr;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Demo.Services;
using PartyKing.Application.Events;
using PartyKing.Application.Sessions;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.Slideshow.Metadata;
using PartyKing.Application.Slideshow.Models;
using PartyKing.Application.Slideshow.Services;
using PartyKing.Contract.V1.Slideshow;
using PartyKing.Domain.Entities.Slideshow;
using PartyKing.Application.Ports;
using PartyKing.Domain.Repositories;

namespace PartyKing.Application.Tests;

public class SlideshowServiceTests
{
    private static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1];

    private readonly FakeClock _clock = new();
    private readonly FakeSlideshowImagesRepository _repository = new();
    private readonly FakeSessionsRepository _sessions = new();
    private readonly FakeSlideshowScheduler _scheduler = new();
    private readonly FakeSlideshowNotifier _notifier = new();
    private readonly FakeMetadataStripper _stripper = new();
    private readonly SlideshowService _service;
    private readonly Guid _sessionId;

    public SlideshowServiceTests()
    {
        _service = CreateService(new DemoConfiguration());
        _sessionId = _sessions.AddSession(hostProfileId: "host").Id;
        _repository.Events = TestEvents.SlideshowDispatcher(_service);
    }

    private SlideshowService CreateService(DemoConfiguration demo)
    {
        var configuration = new SlideshowConfiguration
        {
            UploadedPhotosDirectory = "Images/Uploaded",
            SlideTime = TimeSpan.FromSeconds(5)
        };

        return new SlideshowService(
            _clock,
            _repository,
            _repository,
            new SessionAuthorizer(_sessions),
            Options.Create(configuration),
            new SlideshowWatchers(),
            _scheduler,
            _notifier,
            demo.Enabled
                ? new DemoPhotoQuotaPolicy(Options.Create(demo), _repository)
                : new UnlimitedPhotoQuotaPolicy(),
            _stripper);
    }

    private static SessionCaller Admin => new(null, true);
    private static SessionCaller Host => new("host", false);
    private static SessionCaller Guest => new("someone-else", false);

    private static UploadedFile File(string name, byte[] content) =>
        new(name, content.Length, () => new MemoryStream(content));

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
        _repository.Settings[_sessionId] = new SlideshowSettings(_sessionId, true, TimeSpan.FromSeconds(30), true);
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
        var result = await _service.GetSettingsAsync(_sessionId, Admin, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(TimeSpan.FromSeconds(5), result.Value.SlideTime);
        Assert.True(result.Value.AutoRepeat);
        Assert.True(result.Value.StartFromBeginning);
    }

    [Fact]
    public async Task GetSettings_OfAnUnknownSession_IsNotFound()
    {
        var result = await _service.GetSettingsAsync(Guid.NewGuid(), Admin, CancellationToken.None);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task GetSettings_ForTheHost_ReturnsThem()
    {
        var result = await _service.GetSettingsAsync(_sessionId, Host, CancellationToken.None);

        Assert.False(result.IsError);
    }

    [Fact]
    public async Task GetSettings_ForAGuest_IsForbidden()
    {
        var result = await _service.GetSettingsAsync(_sessionId, Guest, CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
    }

    [Fact]
    public async Task UpdateSettings_ForAGuest_IsForbiddenAndSavesNothing()
    {
        var settings = new SlideshowSettingsDto { SlideTime = TimeSpan.FromSeconds(12) };

        var result = await _service.UpdateSettingsAsync(_sessionId, Guest, settings, CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
        Assert.Empty(_repository.Settings);
    }

    [Fact]
    public async Task UpdateSettings_ForTheHost_SavesThem()
    {
        var settings = new SlideshowSettingsDto { SlideTime = TimeSpan.FromSeconds(12) };

        var result = await _service.UpdateSettingsAsync(_sessionId, Host, settings, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(TimeSpan.FromSeconds(12), _repository.Settings[_sessionId].SlideTime);
    }

    [Fact]
    public async Task UpdateSettings_SavesThemForThatSession()
    {
        var settings = new SlideshowSettingsDto { AutoRepeat = true, SlideTime = TimeSpan.FromSeconds(12) };

        var result = await _service.UpdateSettingsAsync(_sessionId, Admin, settings, CancellationToken.None);

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

        var result = await _service.UpdateSettingsAsync(_sessionId, Admin, settings, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Empty(_repository.Settings);
    }

    [Fact]
    public async Task UpdateSettings_OfAnUnknownSession_IsNotFound()
    {
        var result = await _service.UpdateSettingsAsync(Guid.NewGuid(), Admin, new SlideshowSettingsDto(), CancellationToken.None);

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
    public async Task Upload_WithOneInvalidFile_StoresTheOthers_AndNamesTheRejectedOne()
    {
        var good = File("a.jpg", JpegBytes);
        var bad = File("evil.html", "<script>alert(1)</script>"u8.ToArray());

        var result = await _service.UploadImagesAsync(_sessionId, [good, bad], deleteAfterPresentation: false, CancellationToken.None);

        Assert.Equal(1, result.Value.Stored);
        Assert.Equal(["'evil.html' is not supported. Allowed formats: jpg, jpeg, png, gif, webp."], result.Value.Rejected);
        Assert.Single(_repository.Added);
    }

    [Fact]
    public async Task Upload_WithOnlyInvalidFiles_ReturnsAReasonForEachOfThem()
    {
        var first = File("a.html", "<a>"u8.ToArray());
        var second = File("b.png", "not a png"u8.ToArray());

        var result = await _service.UploadImagesAsync(_sessionId, [first, second], deleteAfterPresentation: false, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(
            [
                "'a.html' is not supported. Allowed formats: jpg, jpeg, png, gif, webp.",
                "'b.png' is not a valid image."
            ],
            result.Errors.Select(x => x.Description));
        Assert.Empty(_repository.Added);
    }

    [Fact]
    public async Task Upload_StoresThePhotoWithoutItsMetadata_NeverTheBytesThatWereUploaded()
    {
        await _service.UploadImagesAsync(_sessionId, [File("a.jpg", JpegBytes)], deleteAfterPresentation: false, CancellationToken.None);

        Assert.Equal(FakeMetadataStripper.Cleaned, Assert.Single(_repository.AddedContent));
        Assert.All(_stripper.Stripped, x => Assert.Equal(JpegBytes, x));
    }

    [Fact]
    public async Task Upload_StripsEachFileOnce_WithItsOwnDetectedFormat()
    {
        await _service.UploadImagesAsync(_sessionId, [File("holiday.JPEG", JpegBytes)], deleteAfterPresentation: false, CancellationToken.None);

        var format = Assert.Single(_stripper.Formats);
        Assert.Equal(".jpg", format.Extension);
    }

    [Fact]
    public async Task Upload_DoesNotStripAFileThatFailedTheChecks()
    {
        await _service.UploadImagesAsync(_sessionId, [File("evil.html", "<a>"u8.ToArray())], deleteAfterPresentation: false, CancellationToken.None);

        Assert.Empty(_stripper.Stripped);
    }

    [Fact]
    public async Task Upload_WithAFileThatCannotBeStripped_StoresTheOthers_AndReportsTheStrippersReason()
    {
        var good = File("a.jpg", JpegBytes);
        var damaged = File("damaged.jpg", [..JpegBytes, FakeMetadataStripper.DamagedMark]);

        var result = await _service.UploadImagesAsync(_sessionId, [damaged, good], deleteAfterPresentation: false, CancellationToken.None);

        Assert.Equal(1, result.Value.Stored);
        Assert.Equal(["The stripper refused 'damaged.jpg'"], result.Value.Rejected);
        Assert.Equal(FakeMetadataStripper.Cleaned, Assert.Single(_repository.AddedContent));
    }

    [Fact]
    public async Task Upload_WithOnlyFilesThatCannotBeStripped_ReturnsTheStrippersReasons()
    {
        var damaged = File("damaged.jpg", [..JpegBytes, FakeMetadataStripper.DamagedMark]);

        var result = await _service.UploadImagesAsync(_sessionId, [damaged], deleteAfterPresentation: false, CancellationToken.None);

        Assert.Equal("The stripper refused 'damaged.jpg'", Assert.Single(result.Errors).Description);
        Assert.Empty(_repository.Added);
    }

    [Fact]
    public async Task Upload_WithNothingStored_DoesNotChangeTheSlide()
    {
        await _service.UploadImagesAsync(_sessionId, [File("evil.html", "<a>"u8.ToArray())], deleteAfterPresentation: false, CancellationToken.None);

        Assert.Empty(_notifier.SlideshowChanges);
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

    [Fact]
    public async Task Watch_ArmsTheTimerAtTheExpirationOfTheSlide()
    {
        _repository.NextImages[_sessionId] = Image("a.jpg");

        await _service.WatchAsync(_sessionId, Admin, "conn", CancellationToken.None);

        Assert.Equal(_clock.UtcNow.AddSeconds(5), _scheduler.Scheduled[_sessionId].At);
    }

    [Fact]
    public async Task Watch_ForAGuest_IsForbiddenAndStartsNothing()
    {
        _repository.NextImages[_sessionId] = Image("a.jpg");

        var result = await _service.WatchAsync(_sessionId, Guest, "conn", CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
        Assert.Empty(_scheduler.Scheduled);
        Assert.Empty(_notifier.SlideshowChanges);
    }

    [Fact]
    public async Task Watch_ForTheHost_ArmsTheTimer()
    {
        var result = await _service.WatchAsync(_sessionId, Host, "conn", CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Contains(_sessionId, _scheduler.Scheduled.Keys);
    }

    [Fact]
    public async Task Watch_OfAnUnknownSession_IsNotFound()
    {
        var result = await _service.WatchAsync(Guid.NewGuid(), Admin, "conn", CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
        Assert.Empty(_scheduler.Scheduled);
    }

    [Fact]
    public async Task Watch_WithAnExpiredSlide_CatchesUpOnceAndNotifies()
    {
        _repository.NextImages[_sessionId] = Image("a.jpg");

        await _service.WatchAsync(_sessionId, Admin, "conn", CancellationToken.None);

        Assert.Equal([_sessionId], _notifier.SlideshowChanges);
    }

    [Fact]
    public async Task Watch_WithASlideStillOnDisplay_DoesNotRotateIt()
    {
        _repository.NextImages[_sessionId] = Image("a.jpg");
        await _service.GetImageAsync(_sessionId, CancellationToken.None);

        _repository.NextImages[_sessionId] = Image("b.jpg");
        await _service.WatchAsync(_sessionId, Admin, "conn", CancellationToken.None);

        Assert.Empty(_notifier.SlideshowChanges);
        Assert.Equal("Images/Uploaded/a.jpg", (await _service.GetImageAsync(_sessionId, CancellationToken.None))!.FileName);
    }

    [Fact]
    public async Task Timer_RotatesTheSlideNotifiesAndArmsTheNextOne()
    {
        _repository.NextImages[_sessionId] = Image("a.jpg");
        await _service.WatchAsync(_sessionId, Admin, "conn", CancellationToken.None);
        _notifier.SlideshowChanges.Clear();

        _clock.Advance(TimeSpan.FromSeconds(6));
        _repository.NextImages[_sessionId] = Image("b.jpg");
        await _scheduler.FireAsync(_sessionId);

        Assert.Equal([_sessionId], _notifier.SlideshowChanges);
        Assert.Equal("Images/Uploaded/b.jpg", (await _service.GetImageAsync(_sessionId, CancellationToken.None))!.FileName);
        Assert.Equal(_clock.UtcNow.AddSeconds(5), _scheduler.Scheduled[_sessionId].At);
    }

    [Fact]
    public async Task Timer_WithTheSameImageAgain_DoesNotNotify()
    {
        _repository.NextImages[_sessionId] = Image("a.jpg");
        await _service.WatchAsync(_sessionId, Admin, "conn", CancellationToken.None);
        _notifier.SlideshowChanges.Clear();

        _clock.Advance(TimeSpan.FromSeconds(6));
        await _scheduler.FireAsync(_sessionId);

        Assert.Empty(_notifier.SlideshowChanges);
    }

    [Fact]
    public async Task Timer_FiringBeforeTheExpiration_RotatesNothing()
    {
        _repository.NextImages[_sessionId] = Image("a.jpg");
        await _service.WatchAsync(_sessionId, Admin, "conn", CancellationToken.None);
        _notifier.SlideshowChanges.Clear();

        _repository.NextImages[_sessionId] = Image("b.jpg");
        await _scheduler.FireAsync(_sessionId);

        Assert.Empty(_notifier.SlideshowChanges);
    }

    [Fact]
    public async Task Timer_FiringAfterTheLastWatcherLeft_RotatesNothing()
    {
        _repository.NextImages[_sessionId] = Image("a.jpg");
        await _service.WatchAsync(_sessionId, Admin, "conn", CancellationToken.None);
        var callback = _scheduler.Scheduled[_sessionId].Callback;
        _service.Unwatch("conn");
        _notifier.SlideshowChanges.Clear();

        _clock.Advance(TimeSpan.FromSeconds(6));
        _repository.NextImages[_sessionId] = Image("b.jpg");
        await callback();

        Assert.Empty(_notifier.SlideshowChanges);
    }

    [Fact]
    public async Task Unwatch_OfTheLastWatcher_CancelsTheTimer()
    {
        await _service.WatchAsync(_sessionId, Admin, "conn", CancellationToken.None);

        _service.Unwatch("conn");

        Assert.Equal([_sessionId], _scheduler.Cancelled);
    }

    [Fact]
    public async Task Unwatch_WhileAnotherConnectionWatches_KeepsTheTimer()
    {
        await _service.WatchAsync(_sessionId, Admin, "one", CancellationToken.None);
        await _service.WatchAsync(_sessionId, Admin, "two", CancellationToken.None);

        _service.Unwatch("one");

        Assert.Empty(_scheduler.Cancelled);
        Assert.True(_scheduler.Scheduled.ContainsKey(_sessionId));
    }

    [Fact]
    public async Task GetImage_OfAWatchedSession_LeavesRotationToTheTimer()
    {
        _repository.NextImages[_sessionId] = Image("a.jpg");
        await _service.WatchAsync(_sessionId, Admin, "conn", CancellationToken.None);

        _clock.Advance(TimeSpan.FromSeconds(6));
        _repository.NextImages[_sessionId] = Image("b.jpg");
        var result = await _service.GetImageAsync(_sessionId, CancellationToken.None);

        Assert.Equal("Images/Uploaded/a.jpg", result!.FileName);
    }

    [Fact]
    public async Task UpdateSettings_RestartsTheSlideOnDisplayWithTheNewSlideTime()
    {
        _repository.NextImages[_sessionId] = Image("a.jpg");
        await _service.WatchAsync(_sessionId, Admin, "conn", CancellationToken.None);
        _clock.Advance(TimeSpan.FromSeconds(2));

        await _service.UpdateSettingsAsync(
            _sessionId,
            Admin,
            new SlideshowSettingsDto { SlideTime = TimeSpan.FromSeconds(30) },
            CancellationToken.None);

        Assert.Equal(_clock.UtcNow.AddSeconds(30), _scheduler.Scheduled[_sessionId].At);
        Assert.Equal(
            _clock.UtcNow.AddSeconds(30),
            (await _service.GetImageAsync(_sessionId, CancellationToken.None))!.ExpirationDate);
    }

    [Fact]
    public async Task Upload_ToAnEmptySlideshow_ShowsThePhotoAtOnceAndNotifies()
    {
        await _service.GetImageAsync(_sessionId, CancellationToken.None);

        _repository.NextImages[_sessionId] = Image("a.jpg");
        await _service.UploadImagesAsync(_sessionId, [File("a.jpg", JpegBytes)], deleteAfterPresentation: false, CancellationToken.None);

        Assert.Equal([_sessionId], _notifier.SlideshowChanges);
        Assert.Equal("Images/Uploaded/a.jpg", (await _service.GetImageAsync(_sessionId, CancellationToken.None))!.FileName);
    }

    [Fact]
    public async Task Upload_WhileAPhotoIsOnDisplay_WaitsForTheNextSlide()
    {
        _repository.NextImages[_sessionId] = Image("a.jpg");
        await _service.GetImageAsync(_sessionId, CancellationToken.None);

        _repository.NextImages[_sessionId] = Image("b.jpg");
        await _service.UploadImagesAsync(_sessionId, [File("b.jpg", JpegBytes)], deleteAfterPresentation: false, CancellationToken.None);

        Assert.Empty(_notifier.SlideshowChanges);
        Assert.Equal("Images/Uploaded/a.jpg", (await _service.GetImageAsync(_sessionId, CancellationToken.None))!.FileName);
    }

    [Fact]
    public async Task DeleteSession_CancelsItsTimer()
    {
        await _service.WatchAsync(_sessionId, Admin, "conn", CancellationToken.None);

        await _service.DeleteSessionAsync(_sessionId);

        Assert.Contains(_sessionId, _scheduler.Cancelled);
    }

    private static DemoConfiguration Demo => new() { Enabled = true, MaxPhotosPerSession = 3, MaxPhotoBytes = 100 };

    [Fact]
    public async Task Upload_InTheDemo_RefusesAPhotoOverTheDemoSize()
    {
        var service = CreateService(Demo);
        var big = new UploadedFile("big.jpg", 101, () => new MemoryStream(JpegBytes));

        var result = await service.UploadImagesAsync(_sessionId, [big], deleteAfterPresentation: false, CancellationToken.None);

        Assert.Equal("In the demo a photo can be at most 0 MB", Assert.Single(result.Errors).Description);
        Assert.Empty(_repository.Added);
    }

    [Fact]
    public async Task Upload_InTheDemo_RefusesABatchThatWouldGoOverThePhotoLimit_AndStoresNothingOfIt()
    {
        _repository.StoredPhotos = 2;
        var service = CreateService(Demo);

        var result = await service.UploadImagesAsync(
            _sessionId,
            [File("a.jpg", JpegBytes), File("b.jpg", JpegBytes)],
            deleteAfterPresentation: false,
            CancellationToken.None);

        Assert.Equal("In the demo a session can hold at most 3 photos", Assert.Single(result.Errors).Description);
        Assert.Empty(_repository.Added);
    }

    [Fact]
    public async Task Upload_InTheDemo_AcceptsTheLastPhotoThatFits()
    {
        _repository.StoredPhotos = 2;
        var service = CreateService(Demo);

        await service.UploadImagesAsync(_sessionId, [File("a.jpg", JpegBytes)], deleteAfterPresentation: false, CancellationToken.None);

        Assert.Single(_repository.Added);
    }

    [Fact]
    public async Task Upload_OutsideTheDemo_HasNoPhotoLimit()
    {
        _repository.StoredPhotos = 1000;

        await _service.UploadImagesAsync(_sessionId, [File("a.jpg", JpegBytes)], deleteAfterPresentation: false, CancellationToken.None);

        Assert.Single(_repository.Added);
    }

    /// <summary>Answers <see cref="Cleaned"/> for every file, and refuses the files that carry <see cref="DamagedMark"/>.</summary>
    private class FakeMetadataStripper : IImageMetadataStripper
    {
        public const byte DamagedMark = 0xDD;
        public static readonly byte[] Cleaned = [0xC1, 0xEA, 0x4E];

        public List<byte[]> Stripped { get; } = [];
        public List<ImageFormat> Formats { get; } = [];

        public async Task<ErrorOr<Stream>> StripAsync(
            Stream content,
            ImageFormat format,
            string fileName,
            CancellationToken cancellationToken)
        {
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);
            var bytes = copy.ToArray();

            lock (Stripped)
            {
                Stripped.Add(bytes);
                Formats.Add(format);
            }

            if (bytes.Contains(DamagedMark))
            {
                return Error.Validation(description: $"The stripper refused '{fileName}'");
            }

            return new MemoryStream(Cleaned);
        }
    }

    private class FakeSlideshowImagesRepository : ISlideshowImagesRepository, ISlideshowSettingsRepository
    {
        public Dictionary<Guid, SlideshowImageReadResult> NextImages { get; } = [];
        public Dictionary<Guid, SlideshowSettings> Settings { get; } = [];

        /// <summary>Receives the events a settings change raised; none until the test wires it.</summary>
        public IDomainEventDispatcher? Events { get; set; }
        public List<SlideshowImageWriteModel> Added { get; } = [];

        /// <summary>What each stored photo held when it was stored (the stream is gone after the upload).</summary>
        public List<byte[]> AddedContent { get; } = [];
        public List<SlideshowImageReadResult> Deleted { get; } = [];
        public List<string> DeletedDirectories { get; } = [];

        public Task InitializeAsync(string rootPath, string uploadedPath, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<SlideshowSettings?> GetSettingsAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(Settings.GetValueOrDefault(sessionId));

        // Runs the real aggregate and dispatches like the real repository does
        public async Task<SlideshowSettingsFailure> ChangeAsync(
            Guid sessionId,
            bool autoRepeat,
            bool startFromBeginning,
            TimeSpan? slideTime,
            TimeSpan defaultSlideTime,
            CancellationToken cancellationToken)
        {
            var settings = Settings.GetValueOrDefault(sessionId) ?? SlideshowSettings.Defaults(sessionId, defaultSlideTime);
            var failure = settings.Change(autoRepeat, startFromBeginning, slideTime, defaultSlideTime);
            if (failure == SlideshowSettingsFailure.None)
            {
                Settings[sessionId] = settings;
                if (Events is not null)
                {
                    await Events.DispatchAsync(settings);
                }
            }

            return failure;
        }

        public Task AddSlideshowImageAsync(SlideshowImageWriteModel slideshowImage, CancellationToken cancellationToken)
        {
            Added.Add(slideshowImage);
            using var copy = new MemoryStream();
            slideshowImage.Data.CopyTo(copy);
            AddedContent.Add(copy.ToArray());
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

        public int StoredPhotos { get; set; }

        public int CountSessionPhotos(string sessionDirectory) => StoredPhotos;

        public Task<string?> CreateSessionArchiveAsync(string sessionDirectory, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }
}
