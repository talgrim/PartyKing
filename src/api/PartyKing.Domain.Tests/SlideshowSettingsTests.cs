using PartyKing.Domain.Entities.Slideshow;
using PartyKing.Domain.Events.Slideshow;

namespace PartyKing.Domain.Tests;

public class SlideshowSettingsTests
{
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly TimeSpan DefaultSlideTime = TimeSpan.FromSeconds(5);

    [Fact]
    public void Defaults_RepeatAndStartFromTheBeginning_WithTheGivenSlideTime()
    {
        var settings = SlideshowSettings.Defaults(SessionId, DefaultSlideTime);

        Assert.Equal(SessionId, settings.SessionId);
        Assert.True(settings.AutoRepeat);
        Assert.True(settings.StartFromBeginning);
        Assert.Equal(DefaultSlideTime, settings.SlideTime);
        Assert.Empty(settings.DrainEvents());
    }

    [Fact]
    public void Change_KeepsAllThreeSettingsAndRaisesTheEvent()
    {
        var settings = SlideshowSettings.Defaults(SessionId, DefaultSlideTime);

        var failure = settings.Change(autoRepeat: false, startFromBeginning: false, TimeSpan.FromSeconds(30), DefaultSlideTime);

        Assert.Equal(SlideshowSettingsFailure.None, failure);
        Assert.False(settings.AutoRepeat);
        Assert.False(settings.StartFromBeginning);
        Assert.Equal(TimeSpan.FromSeconds(30), settings.SlideTime);
        var raised = Assert.Single(settings.DrainEvents());
        Assert.Equal(new SlideshowSettingsChanged(SessionId, false, false, TimeSpan.FromSeconds(30)), raised);
    }

    [Fact]
    public void Change_WithoutASlideTime_UsesTheDefault()
    {
        var settings = SlideshowSettings.Defaults(SessionId, DefaultSlideTime);
        settings.Change(true, true, TimeSpan.FromSeconds(30), DefaultSlideTime);

        settings.Change(true, true, null, DefaultSlideTime);

        Assert.Equal(DefaultSlideTime, settings.SlideTime);
    }

    [Fact]
    public void Change_WithoutASlideTime_DoesNotCheckTheDefaultAgainstTheRange()
    {
        var settings = SlideshowSettings.Defaults(SessionId, DefaultSlideTime);

        var failure = settings.Change(true, true, null, TimeSpan.FromHours(2));

        Assert.Equal(SlideshowSettingsFailure.None, failure);
        Assert.Equal(TimeSpan.FromHours(2), settings.SlideTime);
    }

    [Fact]
    public void Change_ToTheSameValues_StillRaisesTheEvent()
    {
        var settings = SlideshowSettings.Defaults(SessionId, DefaultSlideTime);

        settings.Change(true, true, DefaultSlideTime, DefaultSlideTime);

        Assert.Single(settings.DrainEvents());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3600)]
    public void SlideTimesAtTheEdgesOfTheRange_AreAccepted(int seconds)
    {
        Assert.Equal(
            SlideshowSettingsFailure.None,
            SlideshowSettings.ValidateSlideTime(TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(3601)]
    [InlineData(7200)]
    public void SlideTimesOutsideTheRange_AreRefused(int seconds)
    {
        Assert.Equal(
            SlideshowSettingsFailure.SlideTimeOutOfRange,
            SlideshowSettings.ValidateSlideTime(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void ASlideTimeThatIsLeftOut_IsValid()
    {
        Assert.Equal(SlideshowSettingsFailure.None, SlideshowSettings.ValidateSlideTime(null));
    }

    [Fact]
    public void Change_WithARefusedSlideTime_KeepsTheSettingsAndRaisesNothing()
    {
        var settings = SlideshowSettings.Defaults(SessionId, DefaultSlideTime);

        var failure = settings.Change(false, false, TimeSpan.FromHours(2), DefaultSlideTime);

        Assert.Equal(SlideshowSettingsFailure.SlideTimeOutOfRange, failure);
        Assert.True(settings.AutoRepeat);
        Assert.True(settings.StartFromBeginning);
        Assert.Equal(DefaultSlideTime, settings.SlideTime);
        Assert.Empty(settings.DrainEvents());
    }
}
