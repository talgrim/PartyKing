using PartyKing.Infrastructure.Lyrics.RestApi;

namespace PartyKing.Application.Tests;

public class LrcParserTests
{
    [Fact]
    public void Parse_ReadsTheTimeStampsAndTheText()
    {
        var lines = LrcParser.Parse("[00:12.50] First line\n[01:02.05]Second line\n");

        Assert.Collection(
            lines,
            line => Assert.Equal((12_500, "First line"), (line.TimeMs, line.Text)),
            line => Assert.Equal((62_050, "Second line"), (line.TimeMs, line.Text)));
    }

    [Fact]
    public void Parse_SkipsMetadataTagsAndLinesWithoutATimeStamp()
    {
        var lines = LrcParser.Parse("[ar:Artist]\n[ti:Title]\nplain text\n[00:01.00]Sung\n");

        var line = Assert.Single(lines);
        Assert.Equal("Sung", line.Text);
    }

    [Fact]
    public void Parse_ALineWithSeveralTimeStamps_AppearsOnceForEach_InOrder()
    {
        var lines = LrcParser.Parse("[00:30.00][00:10.00]Chorus\n[00:20.00]Verse");

        Assert.Equal([10_000, 20_000, 30_000], lines.Select(l => l.TimeMs));
        Assert.Equal(["Chorus", "Verse", "Chorus"], lines.Select(l => l.Text));
    }

    [Fact]
    public void Parse_KeepsAnEmptyLine_AsAnInstrumentalBreak()
    {
        var line = Assert.Single(LrcParser.Parse("[00:05.00]"));

        Assert.Equal(string.Empty, line.Text);
    }

    [Theory]
    [InlineData("[00:01.5]x", 1_500)]
    [InlineData("[00:01.05]x", 1_050)]
    [InlineData("[00:01.123]x", 1_123)]
    [InlineData("[00:01]x", 1_000)]
    public void Parse_AcceptsDifferentFractionLengths(string lrc, int expectedMs)
    {
        Assert.Equal(expectedMs, Assert.Single(LrcParser.Parse(lrc)).TimeMs);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_NothingToRead_IsEmpty(string? lrc)
    {
        Assert.Empty(LrcParser.Parse(lrc));
    }
}
