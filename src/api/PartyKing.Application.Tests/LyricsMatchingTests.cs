using PartyKing.Application.Lyrics.Services;
using PartyKing.Domain.Entities.Lyrics;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Enums;

namespace PartyKing.Application.Tests;

public class LyricsMatchingTests
{
    private static readonly IReadOnlyList<LyricsLine> Synced = [new LyricsLine(1000, "la")];

    private static QueueItem Spotify(string title, string artists = "Artist", int? durationMs = 200_000) =>
        new(Guid.NewGuid(), QueueItemSource.Spotify, "spotify:track:1", title, artists, null, durationMs);

    private static QueueItem Youtube(string title, string channel = "Some channel") =>
        new(Guid.NewGuid(), QueueItemSource.Youtube, "https://youtu.be/x", title, channel, null, null);

    private static LyricsCandidate Candidate(
        string track,
        double durationSeconds = 200,
        bool instrumental = false,
        IReadOnlyList<LyricsLine>? lines = null) =>
        new(track, "Artist", durationSeconds, instrumental, lines ?? Synced);

    [Theory]
    [InlineData("Song - Remastered 2011", "Song")]
    [InlineData("Song (feat. Someone)", "Song")]
    [InlineData("Song - Live", "Song")]
    public void Query_FromSpotify_StripsSuffixesFromTheTitle(string title, string expected)
    {
        var query = LyricsQuery.From(Spotify(title, "First, Second"));

        Assert.Equal(expected, query.Title);
        Assert.Equal("First", query.Artist);
        Assert.Equal(200_000, query.DurationMs);
    }

    [Theory]
    [InlineData("Daft Punk - One More Time (Official Video)", "Daft Punk", "One More Time")]
    [InlineData("Artist – Song [Lyrics] HD", "Artist", "Song")]
    [InlineData("Artist | Song", "Artist", "Song")]
    public void Query_FromYoutube_SplitsArtistAndTitle(string videoTitle, string artist, string title)
    {
        var query = LyricsQuery.From(Youtube(videoTitle));

        Assert.Equal(artist, query.Artist);
        Assert.Equal(title, query.Title);
        Assert.Null(query.DurationMs);
    }

    [Fact]
    public void Query_FromYoutube_WithoutASeparator_HasOnlyATitle()
    {
        var query = LyricsQuery.From(Youtube("Just a title (Official Music Video)"));

        Assert.Null(query.Artist);
        Assert.Equal("Just a title", query.Title);
    }

    [Fact]
    public void Pick_TakesTheCandidateWhoseLengthMatches()
    {
        var right = new[] { new LyricsLine(1, "right") };
        var candidates = new[] { Candidate("Song", 260, lines: [new LyricsLine(1, "wrong")]), Candidate("Song", 201, lines: right) };

        Assert.Equal(right, LyricsMatcher.Pick(LyricsQuery.From(Spotify("Song")), candidates));
    }

    [Fact]
    public void Pick_NoCandidateOfTheRightLength_IsNone()
    {
        var candidates = new[] { Candidate("Song", 260), Candidate("Song", 120) };

        Assert.Null(LyricsMatcher.Pick(LyricsQuery.From(Spotify("Song")), candidates));
    }

    [Fact]
    public void Pick_SkipsInstrumentalsAndCandidatesWithoutSyncedLines()
    {
        var candidates = new[] { Candidate("Song", instrumental: true), Candidate("Song", lines: []) };

        Assert.Null(LyricsMatcher.Pick(LyricsQuery.From(Spotify("Song")), candidates));
    }

    [Fact]
    public void Pick_AnotherSong_IsNone()
    {
        Assert.Null(LyricsMatcher.Pick(LyricsQuery.From(Spotify("Song")), [Candidate("Something else")]));
    }

    [Fact]
    public void Pick_IgnoresAccentsAndPunctuationInTheTitle()
    {
        var query = LyricsQuery.From(Spotify("Zażółć gęślą jaźń!"));

        Assert.NotNull(LyricsMatcher.Pick(query, [Candidate("Zazolc gesla jazn")]));
    }

    [Fact]
    public void Pick_YoutubeWithoutALength_MatchesOnTheTitleAlone()
    {
        var query = LyricsQuery.From(Youtube("Artist - Song (Official Video)"));

        Assert.NotNull(LyricsMatcher.Pick(query, [Candidate("Song", 215)]));
        Assert.Null(LyricsMatcher.Pick(query, [Candidate("Other", 215)]));
    }

    [Fact]
    public void Pick_AVeryShortTitle_DoesNotMatchEverythingContainingIt()
    {
        var query = LyricsQuery.From(Spotify("I"));

        Assert.Null(LyricsMatcher.Pick(query, [Candidate("I Want It All")]));
    }
}
