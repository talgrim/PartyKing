namespace PartyKing.Domain.Events.Slideshow;

/// <summary>The session's slideshow settings were saved (also when nothing differs from before).</summary>
public sealed record SlideshowSettingsChanged(Guid SessionId, bool AutoRepeat, bool StartFromBeginning, TimeSpan SlideTime)
    : IDomainEvent;
