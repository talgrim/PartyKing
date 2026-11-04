using PartyKing.Domain.Entities.Base;
using PartyKing.Domain.Events.Slideshow;

namespace PartyKing.Domain.Entities.Slideshow;

/// <summary>Why the settings were refused.</summary>
public enum SlideshowSettingsFailure
{
    None,
    SlideTimeOutOfRange
}

/// <summary>Slideshow settings of one session; a session without a row uses <see cref="Defaults"/>.</summary>
public class SlideshowSettings : AggregateRoot
{
    public static readonly TimeSpan MinSlideTime = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan MaxSlideTime = TimeSpan.FromHours(1);

    private SlideshowSettings()
    {
    }

    internal SlideshowSettings(Guid sessionId, bool autoRepeat, TimeSpan slideTime, bool startFromBeginning)
    {
        SessionId = sessionId;
        AutoRepeat = autoRepeat;
        SlideTime = slideTime;
        StartFromBeginning = startFromBeginning;
    }

    public Guid SessionId { get; internal set; }
    public bool AutoRepeat { get; internal set; }
    public TimeSpan SlideTime { get; internal set; }
    public bool StartFromBeginning { get; internal set; }

    /// <summary>
    /// What a session that never changed its settings gets: repeat, start from the beginning, and the slide time of the
    /// configuration (an argument, the Domain does not read configuration).
    /// </summary>
    public static SlideshowSettings Defaults(Guid sessionId, TimeSpan defaultSlideTime)
    {
        return new SlideshowSettings(sessionId, autoRepeat: true, defaultSlideTime, startFromBeginning: true);
    }

    /// <summary>The rule of <see cref="Change"/>, for a caller that wants to refuse before it looks anything else up.</summary>
    public static SlideshowSettingsFailure ValidateSlideTime(TimeSpan? slideTime)
    {
        if (slideTime is { } value && (value < MinSlideTime || value > MaxSlideTime))
        {
            return SlideshowSettingsFailure.SlideTimeOutOfRange;
        }

        return SlideshowSettingsFailure.None;
    }

    /// <summary>
    /// Saves all three settings at once. A slide time that is left out becomes <paramref name="defaultSlideTime"/>, which is
    /// not checked against the range (it comes from the configuration); one that is given must be within it.
    /// </summary>
    public SlideshowSettingsFailure Change(
        bool autoRepeat,
        bool startFromBeginning,
        TimeSpan? slideTime,
        TimeSpan defaultSlideTime)
    {
        var failure = ValidateSlideTime(slideTime);
        if (failure != SlideshowSettingsFailure.None)
        {
            return failure;
        }

        AutoRepeat = autoRepeat;
        StartFromBeginning = startFromBeginning;
        SlideTime = slideTime ?? defaultSlideTime;
        Raise(new SlideshowSettingsChanged(SessionId, AutoRepeat, StartFromBeginning, SlideTime));

        return SlideshowSettingsFailure.None;
    }
}
