using PartyKing.Application.Lyrics.Services;
using PartyKing.Domain.Entities.Lyrics;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Enums;
using PartyKing.Domain.Results;
using PartyKing.Infrastructure.Lyrics.RestApi;

namespace PartyKing.Application.Tests;

public class LyricsServiceTests
{
    private static readonly Guid SessionId = Guid.NewGuid();

    private readonly FakeQueueRepository _queue = new();
    private readonly FakeLrclibClient _lrclib = new();
    private readonly LyricsService _service;

    public LyricsServiceTests()
    {
        _service = new LyricsService(_queue, _lrclib, new LyricsCache(new FakeClock()));
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

        var result = await _service.GetForPlayingSongAsync(SessionId, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task AnyoneGetsTheLinesOfThePlayingSong()
    {
        _lrclib.Candidates = [Match()];

        var result = await _service.GetForPlayingSongAsync(SessionId, CancellationToken.None);

        Assert.Equal(7, result.Value.SongId);
        Assert.Equal([(1000, "la"), (4000, "lo")], result.Value.Lines.Select(l => (l.TimeMs, l.Text)));
    }

    [Fact]
    public async Task WhenThePreciseSearchFinds_Nothing_TheFreeTextSearchIsTried()
    {
        _lrclib.CandidatesByCall = [[], [Match()]];

        var result = await _service.GetForPlayingSongAsync(SessionId, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(2, _lrclib.Searches);
        Assert.Equal("Artist Song", _lrclib.LastQuery);
    }

    [Fact]
    public async Task NoMatchingLyrics_IsNotFound_AndRememberedSoLrclibIsNotAskedAgain()
    {
        _lrclib.Candidates = [Match("Completely different")];

        var first = await _service.GetForPlayingSongAsync(SessionId, CancellationToken.None);
        var searches = _lrclib.Searches;
        var second = await _service.GetForPlayingSongAsync(SessionId, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, first.FirstError.Type);
        Assert.Equal(ErrorOr.ErrorType.NotFound, second.FirstError.Type);
        Assert.Equal(searches, _lrclib.Searches);
    }

    [Fact]
    public async Task FoundLyrics_AreRemembered()
    {
        _lrclib.Candidates = [Match()];

        await _service.GetForPlayingSongAsync(SessionId, CancellationToken.None);
        var searches = _lrclib.Searches;
        await _service.GetForPlayingSongAsync(SessionId, CancellationToken.None);

        Assert.Equal(searches, _lrclib.Searches);
    }

    [Fact]
    public async Task WhenLrclibIsUnreachable_NothingIsRemembered()
    {
        _lrclib.Unavailable = true;
        var first = await _service.GetForPlayingSongAsync(SessionId, CancellationToken.None);

        _lrclib.Unavailable = false;
        _lrclib.Candidates = [Match()];
        var second = await _service.GetForPlayingSongAsync(SessionId, CancellationToken.None);

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
