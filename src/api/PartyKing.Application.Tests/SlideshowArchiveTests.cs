using System.IO.Compression;
using ErrorOr;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Ports;
using PartyKing.Application.Sessions;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.Slideshow.Services;
using PartyKing.Domain.Entities.Slideshow;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Application.Tests;

public class SlideshowArchiveTests : IDisposable
{
    private const string Uploads = "Images/Uploaded";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"partyking-test-{Guid.NewGuid():N}");
    private readonly FakeSessionsRepository _sessions = new();
    private readonly SlideshowImagesRepository _files;
    private readonly SlideshowArchiveService _service;
    private readonly Guid _sessionId;

    public SlideshowArchiveTests()
    {
        // The archive methods never touch the database
        _files = new SlideshowImagesRepository(null!, null!, NullLogger<SlideshowImagesRepository>.Instance);
        _files.InitializeAsync(_root, Uploads, CancellationToken.None).GetAwaiter().GetResult();
        _service = new SlideshowArchiveService(
            new SessionAuthorizer(_sessions),
            _sessions,
            _files,
            Options.Create(new SlideshowConfiguration { UploadedPhotosDirectory = Uploads, SlideTime = TimeSpan.FromSeconds(5) }));
        _sessionId = _sessions.AddSession(hostProfileId: "host").Id;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private static SessionCaller Host => new("host", false);
    private static SessionCaller Admin => new(null, true);
    private static SessionCaller Guest => new("someone-else", false);

    private string SessionDir => Path.Combine(_root, Uploads, _sessionId.ToString());

    private void AddPhoto(string name, string content)
    {
        Directory.CreateDirectory(SessionDir);
        File.WriteAllText(Path.Combine(SessionDir, name), content);
    }

    [Fact]
    public async Task CreateArchive_ListsPhotosInUploadOrderUnderNumberedNames()
    {
        // GUIDv7 names sort by upload time, whatever the listing order
        AddPhoto("0192-b.png", "second");
        AddPhoto("0191-a.jpg", "first");
        AddPhoto("0193-c.webp", "third");

        var result = await _service.CreateArchiveAsync(_sessionId, Host, CancellationToken.None);

        Assert.False(result.IsError);
        try
        {
            using var zip = ZipFile.OpenRead(result.Value.FilePath);
            Assert.Equal(["001.jpg", "002.png", "003.webp"], zip.Entries.Select(x => x.FullName));
            using var reader = new StreamReader(zip.Entries[0].Open());
            Assert.Equal("first", await reader.ReadToEndAsync());
            Assert.Equal("partyking-ABC123-photos.zip", result.Value.DownloadName);
        }
        finally
        {
            File.Delete(result.Value.FilePath);
        }
    }

    [Fact]
    public async Task CreateArchive_ForAnAdmin_Succeeds()
    {
        AddPhoto("0191-a.jpg", "x");

        var result = await _service.CreateArchiveAsync(_sessionId, Admin, CancellationToken.None);

        Assert.False(result.IsError);
        File.Delete(result.Value.FilePath);
    }

    [Fact]
    public async Task CreateArchive_ForAGuest_IsForbiddenAndWritesNothing()
    {
        AddPhoto("0191-a.jpg", "x");

        var result = await _service.CreateArchiveAsync(_sessionId, Guest, CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
    }

    [Fact]
    public async Task CreateArchive_WithoutPhotos_IsNotFound()
    {
        var result = await _service.CreateArchiveAsync(_sessionId, Host, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task CreateArchive_ForAnUnknownSession_IsNotFound()
    {
        var result = await _service.CreateArchiveAsync(Guid.NewGuid(), Admin, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task CreateArchive_WhenCancelled_LeavesNoFileBehind()
    {
        AddPhoto("0191-a.jpg", "x");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.CreateArchiveAsync(_sessionId, Host, cts.Token));

        var archives = Path.Combine(Path.GetTempPath(), "partyking-archives");
        Assert.True(!Directory.Exists(archives) || Directory.GetFiles(archives).Length == 0);
    }

    [Fact]
    public async Task CountPhotos_CountsFilesForTheHostAndRefusesGuests()
    {
        AddPhoto("0191-a.jpg", "x");
        AddPhoto("0192-b.png", "y");

        var count = await _service.CountPhotosAsync(_sessionId, Host, CancellationToken.None);
        var denied = await _service.CountPhotosAsync(_sessionId, Guest, CancellationToken.None);

        Assert.Equal(2, count.Value.Count);
        Assert.Equal(ErrorType.Forbidden, denied.FirstError.Type);
    }

    [Fact]
    public async Task CountPhotos_WithoutAFolder_IsZero()
    {
        var count = await _service.CountPhotosAsync(_sessionId, Admin, CancellationToken.None);

        Assert.Equal(0, count.Value.Count);
    }
}
