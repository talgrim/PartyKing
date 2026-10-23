namespace PartyKing.Domain.Entities;

/// <summary>Slideshow settings of one session; a session without a row uses the defaults.</summary>
public class SlideshowSettings
{
    public Guid SessionId { get; set; }
    public bool AutoRepeat { get; set; }
    public TimeSpan SlideTime { get; set; }
    public bool StartFromBeginning { get; set; }
}
