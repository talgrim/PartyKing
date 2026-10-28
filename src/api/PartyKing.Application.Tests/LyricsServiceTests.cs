using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Admin.Services;
using PartyKing.Application.Lyrics.Services;
using PartyKing.Application.Player.Services;
using PartyKing.Application.Realtime;
using PartyKing.Application.Sessions.Services;
using PartyKing.Domain.Entities.Lyrics;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Enums;
using PartyKing.Domain.Results;
using PartyKing.Infrastructure.Lyrics.RestApi;

namespace PartyKing.Application.Tests;

public class LyricsServiceTests
{
    private const string HostId = "host-1";

    private readonly FakeSessionsRepository _sessions = new();
    private readonly FakeQueueRepository _queue = new();
    private readonly FakeLrclibClient _lrclib = new();
    private readonly LyricsService _service;
    private readonly Guid _id;

    public LyricsServiceTests()
    {
        var clock = new FakeClock();
        var sessionService = new SessionService(
            _sessions,
            new PasswordHasher(),
            new PlayerStateStore(clock),
            new FakeHostTokenProvider(),
            new FakeSlideshowService(),
            JoinTokens.Create(clock),
            clock,
            new NullSessionMusicNotifier(),
            new NullPlayerConnectionWatchdog(),
            NullLogger<SessionService>.Instance);
        _service = new LyricsService(sessionService, _queue, _lrclib, new LyricsCache(clock));
        _id = _sessions.AddSession(HostId, new PasswordHasher().Hash("secret")).Id;
        _queue.Playing = Song(7, "Song");
    }

    private static QueueItem Song(int id, string title) =>
        new(Guid.NewGuid(), QueueItemSource.Spotify, $"spotify:track:{id}", title, "Artist", null, 200_000) { Id = id };

    private static LyricsCandidate Match(string track = "Song") =>
        new(track, "Artist", 200, false, [new LyricsLine(1000, "la"), new LyricsLine(4000, "lo")]);

    [Fact]
    public async Task Guests_AreForbidden()
    {
        var result = await _service.GetForPlayingSongAsync(_id, "someone-else", false, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.Forbidden, result.FirstError.Type);
        Assert.Equal(0, _lrclib.Searches);
    }

    [Fact]
    public async Task NothingPlaying_IsNotFound()
    {
        _queue.Playing = null;

        var result = await _service.GetForPlayingSongAsync(_id, HostId, false, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task TheHostGetsTheLinesOfThePlayingSong()
    {
        _lrclib.Candidates = [Match()];

        var result = await _service.GetForPlayingSongAsync(_id, HostId, false, CancellationToken.None);

        Assert.Equal(7, result.Value.SongId);
        Assert.Equal([(1000, "la"), (4000, "lo")], result.Value.Lines.Select(l => (l.TimeMs, l.Text)));
    }

    [Fact]
    public async Task AnAdminGetsThemToo()
    {
        _lrclib.Candidates = [Match()];

        Assert.False((await _service.GetForPlayingSongAsync(_id, null, true, CancellationToken.None)).IsError);
    }

    [Fact]
    public async Task WhenThePreciseSearchFinds_Nothing_TheFreeTextSearchIsTried()
    {
        _lrclib.CandidatesByCall = [[], [Match()]];

        var result = await _service.GetForPlayingSongAsync(_id, HostId, false, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(2, _lrclib.Searches);
        Assert.Equal("Artist Song", _lrclib.LastQuery);
    }

    [Fact]
    public async Task NoMatchingLyrics_IsNotFound_AndRememberedSoLrclibIsNotAskedAgain()
    {
        _lrclib.Candidates = [Match("Completely different")];

        var first = await _service.GetForPlayingSongAsync(_id, HostId, false, CancellationToken.None);
        var searches = _lrclib.Searches;
        var second = await _service.GetForPlayingSongAsync(_id, HostId, false, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, first.FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.NotFound, second.FirstError.Type);
        Assert.Equal(searches, _lrclib.Searches);
    }

    [Fact]
    public async Task FoundLyrics_AreRemembered()
    {
        _lrclib.Candidates = [Match()];

        await _service.GetForPlayingSongAsync(_id, HostId, false, CancellationToken.None);
        var searches = _lrclib.Searches;
        await _service.GetForPlayingSongAsync(_id, HostId, false, CancellationToken.None);

        Assert.Equal(searches, _lrclib.Searches);
    }

    [Fact]
    public async Task WhenLrclibIsUnreachable_NothingIsRemembered()
    {
        _lrclib.Unavailable = true;
        var first = await _service.GetForPlayingSongAsync(_id, HostId, false, CancellationToken.None);

        _lrclib.Unavailable = false;
        _lrclib.Candidates = [Match()];
        var second = await _service.GetForPlayingSongAsync(_id, HostId, false, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, first.FirstError.Type);
        Assert.False(second.IsError);
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
