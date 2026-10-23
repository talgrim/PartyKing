using PartyKing.Application.Queue.Helpers;
using PartyKing.Domain.Enums;

namespace PartyKing.Application.Tests;

public class SongInputParserTests
{
    private const string TrackId = "4uLU6hMCjMI75M1A2tKUQC";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("just some text")]
    [InlineData("ftp://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC")]
    [InlineData("https://example.com/watch?v=abc")]
    [InlineData("https://notyoutube.com/watch?v=abc")]
    [InlineData("https://youtube.com.evil.com/watch?v=abc")]
    public void Parse_InvalidInput_ReturnsNull(string? input)
    {
        Assert.Null(SongInputParser.Parse(input));
    }

    [Theory]
    [InlineData("spotify:track:4uLU6hMCjMI75M1A2tKUQC")]
    [InlineData("  spotify:track:4uLU6hMCjMI75M1A2tKUQC  ")]
    [InlineData("https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC")]
    [InlineData("https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC/")]
    [InlineData("https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC?si=abcdef123")]
    [InlineData("http://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC")]
    [InlineData("https://open.spotify.com/intl-pl/track/4uLU6hMCjMI75M1A2tKUQC")]
    [InlineData("https://open.spotify.com/intl-pt-BR/track/4uLU6hMCjMI75M1A2tKUQC")]
    public void Parse_SpotifyTrack_ReturnsTrackId(string input)
    {
        var result = Assert.IsType<SpotifyTrackInput>(SongInputParser.Parse(input));

        Assert.Equal(QueueItemSource.Spotify, result.Source);
        Assert.Equal(TrackId, result.TrackId);
    }

    [Theory]
    [InlineData("spotify:album:4uLU6hMCjMI75M1A2tKUQC")]
    [InlineData("spotify:track:tooShort")]
    [InlineData("spotify:track:4uLU6hMCjMI75M1A2tKUQCextra")]
    [InlineData("https://open.spotify.com/album/4uLU6hMCjMI75M1A2tKUQC")]
    [InlineData("https://open.spotify.com/track/short")]
    [InlineData("https://open.spotify.com/")]
    [InlineData("https://spotify.com/track/4uLU6hMCjMI75M1A2tKUQC")]
    public void Parse_NonTrackSpotifyInput_ReturnsNull(string input)
    {
        Assert.Null(SongInputParser.Parse(input));
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://music.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://WWW.YouTube.com/watch?v=dQw4w9WgXcQ")]
    public void Parse_YoutubeUrl_ReturnsYoutubeInput(string input)
    {
        var result = Assert.IsType<YoutubeInput>(SongInputParser.Parse(input));

        Assert.Equal(QueueItemSource.Youtube, result.Source);
        Assert.Contains("dQw4w9WgXcQ", result.Url);
    }

    [Fact]
    public void Parse_YoutubeUrlWithWhitespace_TrimsInput()
    {
        var result = Assert.IsType<YoutubeInput>(
            SongInputParser.Parse("  https://youtu.be/dQw4w9WgXcQ \n"));

        Assert.Equal("https://youtu.be/dQw4w9WgXcQ", result.Url);
    }
}
