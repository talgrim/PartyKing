using PartyKing.Domain.Entities.Slideshow;

namespace PartyKing.Domain.Tests;

public class SlideshowPlaylistTests
{
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly TimeSpan SlideTime = TimeSpan.FromSeconds(5);

    private static SlideshowSettings Settings(bool autoRepeat = true, bool startFromBeginning = true)
    {
        var settings = SlideshowSettings.Defaults(SessionId, SlideTime);
        settings.Change(autoRepeat, startFromBeginning, SlideTime, SlideTime);

        return settings;
    }

    private static List<SlideshowImage> Images(int count)
    {
        var images = new List<SlideshowImage>();
        for (var i = 0; i < count; i++)
        {
            images.Add(new SlideshowImage(SessionId, ".jpg", "image/jpeg", "Images/Uploaded", deleteAfterPresentation: false));
            // GUIDv7 carries the milliseconds, so two images made at once may not sort in creation order
            Thread.Sleep(2);
        }

        return images;
    }

    [Fact]
    public void Advance_WithoutACurrentImage_ShowsTheFirstOne()
    {
        var images = Images(3);

        var step = SlideshowPlaylist.Of(SessionId, images.AsEnumerable().Reverse()).Advance(null, Settings());

        Assert.Same(images[0], step.Image);
        Assert.False(step.Repeated);
    }

    [Fact]
    public void Advance_ShowsTheImageThatFollowsTheCurrentOne_AndMarksTheCurrentOnePresented()
    {
        var images = Images(3);

        var step = SlideshowPlaylist.Of(SessionId, images).Advance(images[0].Id, Settings());

        Assert.Same(images[1], step.Image);
        Assert.True(images[0].WasPresented);
        Assert.False(images[1].WasPresented);
    }

    [Fact]
    public void Advance_AtTheEnd_StartsOverWhenAutoRepeatIsOn()
    {
        var images = Images(2);

        var step = SlideshowPlaylist.Of(SessionId, images).Advance(images[1].Id, Settings(autoRepeat: true));

        Assert.Same(images[0], step.Image);
        Assert.True(step.Repeated);
    }

    [Fact]
    public void Advance_AtTheEnd_HasNoImageWhenAutoRepeatIsOff()
    {
        var images = Images(2);

        var step = SlideshowPlaylist.Of(SessionId, images).Advance(images[1].Id, Settings(autoRepeat: false));

        Assert.Null(step.Image);
        Assert.False(step.Repeated);
    }

    [Fact]
    public void Advance_WhenStartingFromTheBeginningIsOff_SkipsImagesAlreadyPresented()
    {
        var images = Images(3);
        images[1].MarkPresented();

        var step = SlideshowPlaylist.Of(SessionId, images).Advance(images[0].Id, Settings(startFromBeginning: false));

        Assert.Same(images[2], step.Image);
    }

    [Fact]
    public void Advance_WhenStartingFromTheBeginningIsOff_RepeatsOnlyWhatWasNotPresented()
    {
        var images = Images(2);
        images[0].MarkPresented();

        var step = SlideshowPlaylist.Of(SessionId, images).Advance(images[1].Id, Settings(startFromBeginning: false));

        // Both were presented by now, so there is nothing to start over with
        Assert.Null(step.Image);
        Assert.True(step.Repeated);
    }

    [Fact]
    public void Advance_WhenStartingFromTheBeginningIsOn_ShowsPresentedImagesAgain()
    {
        var images = Images(2);
        images[1].MarkPresented();

        var step = SlideshowPlaylist.Of(SessionId, images).Advance(images[0].Id, Settings(startFromBeginning: true));

        Assert.Same(images[1], step.Image);
    }

    [Fact]
    public void Advance_FromAnImageThatIsGone_ContinuesAfterItsPlace()
    {
        var images = Images(3);
        var gone = images[1];
        images.Remove(gone);

        var step = SlideshowPlaylist.Of(SessionId, images).Advance(gone.Id, Settings());

        Assert.Same(images[1], step.Image);
        Assert.All(images, x => Assert.False(x.WasPresented));
    }

    [Fact]
    public void Advance_WithNoImages_HasNoImageAndDoesNotRepeat()
    {
        var step = SlideshowPlaylist.Of(SessionId, []).Advance(null, Settings());

        Assert.Null(step.Image);
        Assert.False(step.Repeated);
    }

    [Fact]
    public void Of_RefusesAnImageOfAnotherSession()
    {
        var other = new SlideshowImage(Guid.NewGuid(), ".jpg", "image/jpeg", "Images/Uploaded", deleteAfterPresentation: false);

        Assert.Throws<ArgumentException>(() => SlideshowPlaylist.Of(SessionId, [other]));
    }
}
