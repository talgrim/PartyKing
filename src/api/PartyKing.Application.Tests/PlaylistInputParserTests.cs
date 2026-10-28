using PartyKing.Application.Queue.Helpers;

namespace PartyKing.Application.Tests;

public class PlaylistInputParserTests
{
    private const string Id = "37i9dQZF1DXcBWIGoYBM5M";

    [Theory]
    [InlineData($"spotify:playlist:{Id}")]
    [InlineData($"https://open.spotify.com/playlist/{Id}")]
    [InlineData($"https://open.spotify.com/playlist/{Id}?si=abc123")]
    [InlineData($"https://open.spotify.com/intl-pl/playlist/{Id}")]
    [InlineData($"  spotify:playlist:{Id}  ")]
    public void Parse_AcceptsPlaylistLinksAndUris(string input)
    {
        Assert.Equal(Id, PlaylistInputParser.Parse(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("spotify:track:37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("https://open.spotify.com/track/37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("https://open.spotify.com/album/37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("https://example.com/playlist/37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("spotify:playlist:short")]
    [InlineData("just words")]
    public void Parse_RejectsEverythingElse(string? input)
    {
        Assert.Null(PlaylistInputParser.Parse(input));
    }
}
