using PartyKing.Domain.Entities.Lyrics;
using PartyKing.Domain.Events.Lyrics;

namespace PartyKing.Domain.Tests;

public class LyricsOffsetTests
{
    private const string Url = "spotify:track:7";

    private static readonly Guid SessionId = Guid.NewGuid();

    [Fact]
    public void NewOffset_IsZero()
    {
        Assert.Equal(0, new LyricsOffset(Url).OffsetMs);
    }

    [Theory]
    [InlineData(2000)]
    [InlineData(-1500)]
    [InlineData(100_000)]
    [InlineData(-100_000)]
    [InlineData(0)]
    public void Change_KeepsTheOffsetAndRaisesTheEvent(int offsetMs)
    {
        var offset = new LyricsOffset(Url);

        var failure = offset.Change(offsetMs, SessionId, 7);

        Assert.Equal(LyricsOffsetFailure.None, failure);
        Assert.Equal(offsetMs, offset.OffsetMs);
        var raised = Assert.IsType<LyricsOffsetChanged>(Assert.Single(offset.DrainEvents()));
        Assert.Equal(new LyricsOffsetChanged(SessionId, 7, Url, offsetMs), raised);
    }

    [Fact]
    public void Change_ToTheSameValue_StillRaisesTheEvent()
    {
        var offset = new LyricsOffset(Url);
        offset.Change(500, SessionId, 7);
        offset.DrainEvents();

        offset.Change(500, SessionId, 7);

        Assert.Single(offset.DrainEvents());
    }

    [Theory]
    [InlineData(100_001)]
    [InlineData(-100_001)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Change_BeyondTheRange_IsRefusedAndKeepsTheOffset(int offsetMs)
    {
        var offset = new LyricsOffset(Url);
        offset.Change(300, SessionId, 7);
        offset.DrainEvents();

        var failure = offset.Change(offsetMs, SessionId, 7);

        Assert.Equal(LyricsOffsetFailure.OutOfRange, failure);
        Assert.Equal(300, offset.OffsetMs);
        Assert.Empty(offset.DrainEvents());
    }

    [Theory]
    [InlineData(100_000, LyricsOffsetFailure.None)]
    [InlineData(-100_000, LyricsOffsetFailure.None)]
    [InlineData(100_001, LyricsOffsetFailure.OutOfRange)]
    [InlineData(-100_001, LyricsOffsetFailure.OutOfRange)]
    public void Validate_AppliesTheSameRuleAsChange(int offsetMs, LyricsOffsetFailure expected)
    {
        Assert.Equal(expected, LyricsOffset.Validate(offsetMs));
    }
}
