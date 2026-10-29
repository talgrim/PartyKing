using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Admin.Services;
using PartyKing.Application.Lyrics.Services;
using PartyKing.Application.Player.Services;
using PartyKing.Application.Realtime;
using PartyKing.Application.Sessions.Services;
using PartyKing.Contract.V1.Lyrics;
using PartyKing.Domain.Entities.Lyrics;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Enums;
using PartyKing.Domain.Results;
using PartyKing.Infrastructure.Lyrics.RestApi;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Application.Tests;

public class LyricsServiceTests
{
    private const string HostId = "host-1";

    private readonly FakeQueueRepository _queue = new();
    private readonly FakeLrclibClient _lrclib = new();
    private readonly FakeLyricsOffsetsRepository _offsets = new();
    private readonly FakeSessionMusicNotifier _notifier = new();
    private readonly LyricsService _service;
    private readonly Guid _sessionId;

    public LyricsServiceTests()
    {
        var clock = new FakeClock();
        var sessions = new FakeSessionsRepository();
        _sessionId = sessions.AddSession(HostId).Id;
        var sessionService = new SessionService(
            sessions,
            new PasswordHasher(),
            new PlayerStateStore(clock),
            new FakeHostTokenProvider(),
            new FakeSlideshowService(),
            JoinTokens.Create(clock),
            clock,
            new NullSessionMusicNotifier(),
            new NullPlayerConnectionWatchdog(),
            NullLogger<SessionService>.Instance);
        _service = new LyricsService(_queue, _lrclib, new LyricsCache(clock), _offsets, sessionService, _notifier);
        _queue.Playing = Song(7, "Song");
    }

    private static QueueItem Song(int id, string title) =>
        new(Guid.NewGuid(), QueueItemSource.Spotify, $"spotify:track:{id}", title, "Artist", null, 200_000) { Id = id };

    private static LyricsCandidate Match(string track = "Song") =>
        new(track, "Artist", 200, false, [new LyricsLine(1000, "la"), new LyricsLine(4000, "lo")]);

    [Fact]
    public async Task NothingPlaying_IsNotFound()
    {
        _queue.Playing = null;

        var result = await _service.GetForPlayingSongAsync(_sessionId, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task AnyoneGetsTheLinesOfThePlayingSong()
    {
        _lrclib.Candidates = [Match()];

        var result = await _service.GetForPlayingSongAsync(_sessionId, CancellationToken.None);

        Assert.Equal(7, result.Value.SongId);
        Assert.Equal([(1000, "la"), (4000, "lo")], result.Value.Lines.Select(l => (l.TimeMs, l.Text)));
    }

    [Fact]
    public async Task WhenThePreciseSearchFinds_Nothing_TheFreeTextSearchIsTried()
    {
        _lrclib.CandidatesByCall = [[], [Match()]];

        var result = await _service.GetForPlayingSongAsync(_sessionId, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(2, _lrclib.Searches);
        Assert.Equal("Artist Song", _lrclib.LastQuery);
    }

    [Fact]
    public async Task NoMatchingLyrics_IsNotFound_AndRememberedSoLrclibIsNotAskedAgain()
    {
        _lrclib.Candidates = [Match("Completely different")];

        var first = await _service.GetForPlayingSongAsync(_sessionId, CancellationToken.None);
        var searches = _lrclib.Searches;
        var second = await _service.GetForPlayingSongAsync(_sessionId, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, first.FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.NotFound, second.FirstError.Type);
        Assert.Equal(searches, _lrclib.Searches);
    }

    [Fact]
    public async Task FoundLyrics_AreRemembered()
    {
        _lrclib.Candidates = [Match()];

        await _service.GetForPlayingSongAsync(_sessionId, CancellationToken.None);
        var searches = _lrclib.Searches;
        await _service.GetForPlayingSongAsync(_sessionId, CancellationToken.None);

        Assert.Equal(searches, _lrclib.Searches);
    }

    [Fact]
    public async Task WhenLrclibIsUnreachable_NothingIsRemembered()
    {
        _lrclib.Unavailable = true;
        var first = await _service.GetForPlayingSongAsync(_sessionId, CancellationToken.None);

        _lrclib.Unavailable = false;
        _lrclib.Candidates = [Match()];
        var second = await _service.GetForPlayingSongAsync(_sessionId, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, first.FirstError.Type);
        Assert.False(second.IsError);
    }

    [Fact]
    public async Task TheSavedOffset_IsReturnedWithTheLyrics()
    {
        _lrclib.Candidates = [Match()];
        _offsets.Saved["spotify:track:7"] = -1500;

        var result = await _service.GetForPlayingSongAsync(_sessionId, CancellationToken.None);

        Assert.Equal(-1500, result.Value.OffsetMs);
    }

    [Fact]
    public async Task WithoutASavedOffset_ItIsZero()
    {
        _lrclib.Candidates = [Match()];

        var result = await _service.GetForPlayingSongAsync(_sessionId, CancellationToken.None);

        Assert.Equal(0, result.Value.OffsetMs);
    }

    [Theory]
    [InlineData(HostId, false)]
    [InlineData(null, true)]
    public async Task TheHostAndAnAdmin_CanSaveTheOffset_AndTheSessionIsNotified(string? profileId, bool isAdmin)
    {
        var result = await _service.SetOffsetAsync(_sessionId, 2000, profileId, isAdmin, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(2000, _offsets.Saved["spotify:track:7"]);
        var (sessionId, change) = Assert.Single(_notifier.LyricsOffsetChanges);
        Assert.Equal(_sessionId, sessionId);
        Assert.Equal(new LyricsOffsetChangedDto(7, 2000), change);
    }

    [Fact]
    public async Task ZeroIsStoredLikeAnyOtherOffset()
    {
        _offsets.Saved["spotify:track:7"] = 500;

        await _service.SetOffsetAsync(_sessionId, 0, HostId, false, CancellationToken.None);

        Assert.Equal(0, _offsets.Saved["spotify:track:7"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("someone-else")]
    public async Task AnyoneElse_CannotSaveTheOffset(string? profileId)
    {
        var result = await _service.SetOffsetAsync(_sessionId, 2000, profileId, false, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.Forbidden, result.FirstError.Type);
        Assert.Empty(_offsets.Saved);
        Assert.Empty(_notifier.LyricsOffsetChanges);
    }

    [Fact]
    public async Task SavingTheOffset_WhenNothingPlays_IsNotFound()
    {
        _queue.Playing = null;

        var result = await _service.SetOffsetAsync(_sessionId, 2000, HostId, false, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
        Assert.Empty(_offsets.Saved);
    }

    [Theory]
    [InlineData(100_001)]
    [InlineData(-100_001)]
    public async Task AnOffsetOutOfRange_IsRejected(int offsetMs)
    {
        var result = await _service.SetOffsetAsync(_sessionId, offsetMs, HostId, false, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.Validation, result.FirstError.Type);
        Assert.Empty(_offsets.Saved);
    }

    private sealed class FakeLyricsOffsetsRepository : ILyricsOffsetsRepository
    {
        public Dictionary<string, int> Saved { get; } = [];

        public Task<int> GetAsync(string songUrl, CancellationToken cancellationToken) =>
            Task.FromResult(Saved.GetValueOrDefault(songUrl));

        public Task SaveAsync(string songUrl, int offsetMs, CancellationToken cancellationToken)
        {
            Saved[songUrl] = offsetMs;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLrclibClient : ILrclibClient
    {
        public IReadOnlyList<LyricsCandidate> Candidates { get; set; } = [];
        public IReadOnlyList<IReadOnlyList<LyricsCandidate>>? CandidatesByCall { get; set; }
        public bool Unavailable { get; set; }
        public int Searches { get; private set; }
        public string? LastQuery { get; private set; }

        public Task<LyricsSearchResult> SearchAsync(
            string? trackName,
            string? artistName,
            string? query,
            CancellationToken cancellationToken)
        {
            var call = Searches++;
            LastQuery = query;

            if (Unavailable)
            {
                return Task.FromResult(LyricsSearchResult.Unavailable);
            }

            var candidates = CandidatesByCall is { } byCall ? byCall[Math.Min(call, byCall.Count - 1)] : Candidates;
            return Task.FromResult(new LyricsSearchResult(LyricsSearchStatus.Completed, candidates));
        }
    }
}
